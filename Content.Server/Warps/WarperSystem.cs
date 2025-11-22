using Content.Server.Ghost.Components;
using Content.Server.Popups;
using Content.Shared.Ghost;
using Content.Shared.Interaction;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using System.Numerics;

namespace Content.Server.Warps;

public class WarperSystem : EntitySystem
{
    [Dependency] private readonly PopupSystem _popupSystem = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly WarpPointSystem _warpPointSystem = default!;
    [Dependency] private readonly SharedTransformSystem _sharedTransform = default!;
    [Dependency] private readonly PullingSystem _pullingSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WarperComponent, InteractHandEvent>(OnInteractHand);
    }

    private void OnInteractHand(EntityUid uid, WarperComponent component, InteractHandEvent args)
    {
        if (component.ID is null)
        {
            Logger.DebugS("warper", "Warper has no destination");
            _popupSystem.PopupEntity(Loc.GetString("warper-goes-nowhere", ("warper", args.Target)), args.User, Filter.Entities(args.User), true);
            return;
        }

        var dest = _warpPointSystem.FindWarpPoint(component.ID);
        if (dest is null)
        {
            Logger.DebugS("warper", String.Format("Warp destination '{0}' not found", component.ID));
            _popupSystem.PopupEntity(Loc.GetString("warper-goes-nowhere", ("warper", args.Target)), args.User, Filter.Entities(args.User), true);
            return;
        }

        var entMan = IoCManager.Resolve<IEntityManager>();
        TransformComponent? destXform;
        entMan.TryGetComponent<TransformComponent>(dest.Value, out destXform);
        if (destXform is null)
        {
            Logger.DebugS("warper", String.Format("Warp destination '{0}' has no transform", component.ID));
            _popupSystem.PopupEntity(Loc.GetString("warper-goes-nowhere", ("warper", args.Target)), args.User, Filter.Entities(args.User), true);
            return;
        }

        // Check that the destination map is initialized and return unless in aghost mode.
        var mapMgr = IoCManager.Resolve<IMapManager>();
        var destMap = destXform.MapID;
        if (!mapMgr.IsMapInitialized(destMap) || mapMgr.IsMapPaused(destMap))
        {
            if (!entMan.HasComponent<GhostComponent>(args.User))
            {
                // Normal ghosts cannot interact, so if we're here this is already an admin ghost.
                Logger.DebugS("warper", String.Format("Player tried to warp to '{0}', which is not on a running map", component.ID));
                _popupSystem.PopupEntity(Loc.GetString("warper-goes-nowhere", ("warper", args.Target)), args.User, Filter.Entities(args.User), true);
                return;
            }
        }

        // Forge-Change-Start
        if (TryComp(args.User, out PullerComponent? puller) && puller.Pulling != null)
        {
            var pullerItem = puller.Pulling.Value;
            _sharedTransform.SetCoordinates(pullerItem, destXform.Coordinates);
            _sharedTransform.AttachToGridOrMap(pullerItem);
            _sharedTransform.SetCoordinates(args.User, destXform.Coordinates);
            _sharedTransform.AttachToGridOrMap(args.User);
            _pullingSystem.TryStartPull(args.User, pullerItem); // Срёт ошибкой на клиенте, не критично, но не приятно.
        }

        else
        {
            _sharedTransform.SetCoordinates(args.User, destXform.Coordinates);
            _sharedTransform.AttachToGridOrMap(args.User);
        }

        if (HasComp<PhysicsComponent>(args.User))
        {
            _physics.SetLinearVelocity(args.User, Vector2.Zero);
        }
        // Forge-Change-End
    }
}
