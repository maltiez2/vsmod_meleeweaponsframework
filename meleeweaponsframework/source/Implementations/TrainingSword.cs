using System.Drawing;
using System.Numerics;
using System.Reflection.Metadata;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace MeleeWeaponsFramework;

public class TrainingSword : DirectionalWeapon
{
    public override void OnLoaded(ICoreAPI api)
    {
        base.OnLoaded(api);
        
        MeleeWeaponsFrameworkModSystem system = api.ModLoader.GetModSystem<MeleeWeaponsFrameworkModSystem>();
        _entityHitParticles = system.ParticleEffectsManager?.Get(_hitParticles);
    }

    protected override void OnAttackStart(ItemSlot slot, EntityPlayer player, ref MeleeWeaponState state, ActionEventData eventData, bool mainHand, AttackDirection direction)
    {
        _entitiesHit.Clear();
    }

    protected override void OnAttackCallback(AttackResult result, ItemSlot slot, AttackDirection direction, bool mainHand)
    {
        base.OnAttackCallback(result, slot, direction, mainHand);

        foreach ((Entity entity, Vector3 point, _) in result.Entities.Where(element => !_entitiesHit.Contains(element.entity.EntityId)))
        {
            Api?.World.PlaySoundAt(_hitSound, Api.World.Player.Entity);
            _entitiesHit.Add(entity.EntityId);

            if (_entityHitParticles != null)
            {
                _entityHitParticles.basePos.X = point.X;
                _entityHitParticles.basePos.Y = point.Y;
                _entityHitParticles.basePos.Z = point.Z;
                Api?.World.SpawnParticles(_entityHitParticles);
            }
        }
    }

    private readonly AssetLocation _hitSound = new("game:sounds/held/shieldblock");
    private readonly HashSet<long> _entitiesHit = new();
    private const string _hitParticles = "meleeweaponsframework:entity-hit-success";
    private AdvancedParticleProperties? _entityHitParticles;
}
