    using Content.Shared._NC.Currency;
    using Content.Server.Stack;
    using Content.Shared.Containers.ItemSlots;
    using Content.Shared.Stacks;
    using Content.Shared.Inventory;
    using Content.Shared.Hands.Components;
    using Content.Shared.Hands.EntitySystems;
    using Content.Shared.Storage;
    using Robust.Shared.Prototypes;
    using Robust.Shared.Containers;
    using Robust.Shared.Map;


    namespace Content.Server._NC.Currency;

    public sealed class CapCoinHandlerSystem : EntitySystem, ICurrencyHandler
    {
        [Dependency] private readonly IEntityManager _ents = default!;
        [Dependency] private readonly IPrototypeManager _protos = default!;
        [Dependency] private readonly StackSystem _stacks = default!;
        [Dependency] private readonly InventorySystem _inventory = default!;
        [Dependency] private readonly SharedHandsSystem _hands = default!;
        [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;

        private static readonly ISawmill Sawmill = Logger.GetSawmill("capcoin");


        public string Id => "CapCoin";
        private string _entityProto = default!;
        private string? _stackType;
        private StackPrototype? _stackProto;

        public override void Initialize()
        {
            base.Initialize();

            if (!_protos.TryIndex<CurrencyPrototype>(Id, out var proto))
                return;

            _entityProto = proto.Entity;

            if (_protos.TryIndex<EntityPrototype>(_entityProto, out var entProto) &&
                entProto.TryGetComponent(out StackComponent? stack, IoCManager.Resolve<IComponentFactory>()))
            {
                _stackType = stack.StackTypeId;
                if (_protos.TryIndex<StackPrototype>(_stackType, out var stackProto))
                    _stackProto = stackProto;
            }

            CurrencyRegistry.Register(this);
        }

        public bool CanAfford(EntityUid owner, int amount)
        {
            return GetBalance(owner) >= amount;
        }
        private int GetContainerSlotLimit(BaseContainer container)
        {
            if (container is ContainerSlot)
                return 1;
            if (_ents.TryGetComponent(container.Owner, out StorageComponent? storage))
                return storage.Grid.GetArea(); // StorageHelpers.GetArea

            return int.MaxValue;
        }
        public int GetBalance(EntityUid owner)
        {
            var total = 0;
            foreach (var item in CurrencyHelpers.EnumerateDeepItems(owner, _ents))
                if (_ents.TryGetComponent(item, out CurrencyItemComponent? coin) && coin.Currency == Id)
                    total += coin.Amount;
                else if (_stackType != null &&
                    _ents.TryGetComponent(item, out StackComponent? st) &&
                    st.StackTypeId == _stackType)
                    total += st.Count;
            return total;
        }

        public CurrencyOpResult Debit(EntityUid owner, int amount)
        {
            if (amount <= 0)
                return CurrencyOpResult.Invalid;

            if (GetBalance(owner) < amount)
                return CurrencyOpResult.InsufficientFunds;

            // Планируем удаление
            var plan = new List<(EntityUid item, int take, bool isStack)>();
            var left = amount;

            foreach (var item in CurrencyHelpers.EnumerateDeepItems(owner, _ents))
            {
                if (left <= 0)
                    break;

                if (_ents.TryGetComponent(item, out CurrencyItemComponent? coin) && coin.Currency == Id)
                {
                    var take = Math.Min(coin.Amount, left);
                    plan.Add((item, take, false));
                    left -= take;
                }
                else if (_stackType != null &&
                         _ents.TryGetComponent(item, out StackComponent? st) &&
                         st.StackTypeId == _stackType)
                {
                    var take = Math.Min(st.Count, left);
                    plan.Add((item, take, true));
                    left -= take;
                }
            }

            if (left > 0)
            {
                Sawmill.Warning($"[Debit] Не смогли покрыть сумму {amount} для {ToPrettyString(owner)}");
                return CurrencyOpResult.Invalid;
            }

            foreach (var (item, take, isStack) in plan)
                if (isStack)
                {
                    var stack = _ents.GetComponent<StackComponent>(item);
                    _stacks.SetCount(item, stack.Count - take, stack);
                    if (stack.Count - take == 0)
                        _ents.DeleteEntity(item);
                }
                else
                {
                    var coin = _ents.GetComponent<CurrencyItemComponent>(item);
                    coin.Amount -= take;
                    if (coin.Amount == 0)
                        _ents.DeleteEntity(item);
                }

            return CurrencyOpResult.Success;
        }

        public CurrencyOpResult Credit(EntityUid owner, int amount)
        {
            if (amount <= 0)
                return CurrencyOpResult.Invalid;

            var coords = _ents.GetComponent<TransformComponent>(owner).Coordinates;
            var left = FillPartialStacks(owner, amount);

            if (!HasAvailableSlotOrContainer(owner))
            {
                Sawmill.Warning($"[Credit] Нет места для получения валюты у {ToPrettyString(owner)} (нет ни контейнера, ни инвентаря, ни рук)");
                return CurrencyOpResult.Invalid;
            }

            var fail = false;
            var spawnedEntities = new List<EntityUid>();

            if (_stackProto != null)
            {
                if (left > 0)
                    fail = !TryInsertOrDrop(owner, _stackProto, left, spawnedEntities, coords);
            }
            else
            {
                for (var i = 0; i < left; i++)
                {
                    var ent = _ents.SpawnEntity(_entityProto, coords);
                    spawnedEntities.Add(ent);
                    if (!TryInsertIntoBestSlot(owner, ent))
                    {
                        _ents.System<SharedTransformSystem>().SetCoordinates(ent, coords);
                        Sawmill.Warning($"[Credit] Валюта {ent} выдана на пол, так как не удалось вставить в контейнер/руки {ToPrettyString(owner)}");
                    }
                }
            }

            if (fail)
            {
                foreach (var ent in spawnedEntities)
                    if (_ents.EntityExists(ent))
                        _ents.DeleteEntity(ent);
                Sawmill.Error($"[Credit] Не удалось выдать всю сумму {amount} для {ToPrettyString(owner)}, возможна потеря валюты!");
                return CurrencyOpResult.Invalid;
            }

            return CurrencyOpResult.Success;
        }


        private bool HasAvailableSlotOrContainer(EntityUid owner)
        {
            if (_ents.TryGetComponent(owner, out ItemSlotsComponent? itemSlots))
            {
                foreach (var slot in itemSlots.Slots.Values)
                    if (!slot.Locked && !slot.HasItem)
                        return true;
            }

            if (_ents.TryGetComponent(owner, out InventoryComponent? inventory))
            {
                foreach (var slot in inventory.Slots)
                    if (!_inventory.TryGetSlotEntity(owner, slot.Name, out _))
                        return true;
            }

            if (_ents.TryGetComponent(owner, out ContainerManagerComponent? containers))
            {
                foreach (var container in containers.Containers.Values)
                {
                    int limit = GetContainerSlotLimit(container);
                    if (container.ContainedEntities.Count < limit)
                        return true;
                }
            }

            if (_ents.TryGetComponent(owner, out HandsComponent? hands))
            {
                foreach (var hand in _hands.EnumerateHands(owner, hands))
                    if (hand.HeldEntity == null)
                        return true;
            }
            return false;
        }





        private bool TryInsertOrDrop(EntityUid owner, StackPrototype stackProto, int amount, List<EntityUid> spawnedEntities, EntityCoordinates coords)
        {
            var spawned = _stacks.Spawn(amount, stackProto, coords);
            spawnedEntities.Add(spawned);

            if (TryInsertIntoBestSlot(owner, spawned))
                return true;

            _ents.GetComponent<TransformComponent>(spawned);
            _ents.System<SharedTransformSystem>().SetCoordinates(spawned, coords);
            Sawmill.Warning($"[Credit] Stack {spawned} выдан на пол, не удалось вставить в контейнер/руки {ToPrettyString(owner)}");
            return true;
        }

        private bool TryInsertIntoBestSlot(EntityUid owner, EntityUid entity)
        {
            if (_ents.TryGetComponent(owner, out ItemSlotsComponent? itemSlots))
            {
                foreach (var slot in itemSlots.Slots.Values)
                {
                    if (slot.Locked || slot.HasItem)
                        continue;
                    if (_itemSlots.TryInsert(owner, slot, entity, owner))
                        return true;
                }
            }

            // 2. Inventory (рюкзак, пояс, слоты одежды)
            if (_ents.TryGetComponent(owner, out InventoryComponent? inventory))
            {
                foreach (var slot in inventory.Slots)
                    if (_inventory.TryEquip(owner, owner, entity, slot.Name, false, false, false, inventory))
                        return true;
            }

            // 3. Руки
            if (_ents.TryGetComponent(owner, out HandsComponent? _))
            {
                if (_hands.TryPickupAnyHand(owner, entity, false))
                    return true;
            }

            // 4. — никуда не вставилось, предмет останется лежать на полу (coords = под владельцем)
            return false;
        }

        private int FillPartialStacks(EntityUid owner, int amount)
        {
            foreach (var item in CurrencyHelpers.EnumerateDeepItems(owner, _ents))
                if (_ents.TryGetComponent(item, out StackComponent? stack) &&
                    stack.StackTypeId == _stackType &&
                    stack.Count < _stackProto!.MaxCount.GetValueOrDefault(int.MaxValue))
                {
                    var maxCount = _stackProto.MaxCount.GetValueOrDefault(int.MaxValue);
                    var space = maxCount - stack.Count;
                    var toAdd = Math.Min(space, amount);
                    _stacks.SetCount(item, stack.Count + toAdd, stack);
                    amount -= toAdd;
                }
            return amount;
        }
    }

