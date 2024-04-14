using AnimationManagerLib;
using AnimationManagerLib.API;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace MeleeWeaponsFramework;

public readonly struct PlayerAnimationData
{
    public readonly AnimationId TpHands;
    public readonly AnimationId FpHands;
    public readonly AnimationId TpLegs;
    public readonly AnimationId FpLegs;

    public const float DefaultHandsCategoryWeight = 512f;
    public const float DefaultLegsCategoryWeight = 16f;

    public PlayerAnimationData(string code, IAnimationManagerSystem system, float easeInFrame = 0f)
    {
        string tpHandsCode = $"{code}-tp-hands";
        string fpHandsCode = $"{code}-fp-hands";
        string tpLegsCode = $"{code}-tp-legs";
        string fpLegsCode = $"{code}-fp-legs";

        TpHands = new("MeleeWeaponsFramework:TpHands", tpHandsCode, EnumAnimationBlendMode.Average, DefaultHandsCategoryWeight);
        FpHands = new("MeleeWeaponsFramework:FpHands", fpHandsCode, EnumAnimationBlendMode.Average, DefaultHandsCategoryWeight);
        TpLegs = new("MeleeWeaponsFramework:TpLegs", tpLegsCode, EnumAnimationBlendMode.Average, DefaultLegsCategoryWeight);
        FpLegs = new("MeleeWeaponsFramework:FpLegs", fpLegsCode, EnumAnimationBlendMode.Average, DefaultLegsCategoryWeight);

        AnimationData tpHandsData = AnimationData.Player(tpHandsCode);
        AnimationData fpHandsData = AnimationData.Player(fpHandsCode);
        AnimationData tpLegsData = AnimationData.Player(tpLegsCode);
        AnimationData fpLegsData = AnimationData.Player(fpLegsCode);

        system.Register(FpLegs, fpLegsData);
        system.Register(FpHands, fpHandsData);
        system.Register(TpLegs, tpLegsData);
        system.Register(TpHands, tpHandsData);

        _frame = easeInFrame;
    }

    public void Start(Entity entity, IAnimationManagerSystem system, TimeSpan easeInTime)
    {
        RunParameters parameters = RunParameters.EaseIn(easeInTime, _frame, ProgressModifierType.Sin);

        Start(entity, system, parameters);
    }
    public void Start(Entity entity, IAnimationManagerSystem system, RunParameters parameters)
    {
        system.Run(new(entity.EntityId, AnimationTargetType.EntityThirdPerson), new(TpHands, parameters), synchronize: true);
        system.Run(new(entity.EntityId, AnimationTargetType.EntityFirstPerson), new(FpHands, parameters), synchronize: false);
        system.Run(new(entity.EntityId, AnimationTargetType.EntityThirdPerson), new(TpLegs, parameters), synchronize: true);
        system.Run(new(entity.EntityId, AnimationTargetType.EntityFirstPerson), new(FpLegs, parameters), synchronize: false);
    }

    public void Stop(Entity entity, IAnimationManagerSystem system, TimeSpan easeOutTime)
    {
        RunParameters parameters = RunParameters.EaseOut(easeOutTime, ProgressModifierType.Sin);

        system.Run(new(entity.EntityId, AnimationTargetType.EntityThirdPerson), new(TpHands, parameters), synchronize: true);
        system.Run(new(entity.EntityId, AnimationTargetType.EntityFirstPerson), new(FpHands, parameters), synchronize: false);
        system.Run(new(entity.EntityId, AnimationTargetType.EntityThirdPerson), new(TpLegs, parameters), synchronize: true);
        system.Run(new(entity.EntityId, AnimationTargetType.EntityFirstPerson), new(FpLegs, parameters), synchronize: false);
    }

    public static PlayerAnimationData Empty(IAnimationManagerSystem system)
    {
        _empty ??= new("empty", system);
        return _empty.Value;
    }

    private static PlayerAnimationData? _empty;
    private readonly float _frame = 0f;
}

internal class MeleeWeaponPlayerBehavior : EntityBehavior
{
    public TimeSpan AnimationsEaseOutTime = TimeSpan.FromMilliseconds(500);
    public TimeSpan DefaultIdleAnimationDelay = TimeSpan.FromSeconds(5);

    public MeleeWeaponPlayerBehavior(Entity entity) : base(entity)
    {
        if (entity is not EntityPlayer player)
        {
            throw new ArgumentException($"This behavior should only be applied to player");
        }

        _player = player;
        _api = entity.Api;

        if (_api is not ICoreClientAPI clientApi) return;

        _clientApi = clientApi;
        _clientSide = true;

        MeleeWeaponsFramework frameworkSystem = _api.ModLoader.GetModSystem<MeleeWeaponsFramework>();
        _animationSystem = _api.ModLoader.GetModSystem<AnimationManagerLibSystem>();
    }
    public override string PropertyName() => _statCategory;
    public override void OnGameTick(float deltaTime)
    {
        if (_clientSide) _ = CheckIfItemsInHandsChanged();
    }

    /// <summary>
    /// Plays player model animations for main hand and off hand items.
    /// </summary>
    /// <param name="animation"></param>
    /// <param name="mainHand"></param>
    /// <param name="idleAnimationDelay">If <see cref="null"/> default value is used, if <see cref="TimeSpan.Zero"/> idle animation wont play, else idle animation will be player after specified time if no animations are played before.</param>
    public void PlayAnimation(PlayerAnimationData animation, bool mainHand = true, TimeSpan? idleAnimationDelay = null)
    {
        if (!_clientSide)
        {
            throw new InvalidOperationException("This method should be called only on client side");
        }
        ResetIdleTimer(idleAnimationDelay);

        if (mainHand)
        {
            animation.Start(entity, _animationSystem, AnimationsEaseOutTime);
            _currentMainHandAnimation = animation;
        }
        else
        {
            animation.Start(entity, _animationSystem, AnimationsEaseOutTime);
            _currentOffHandAnimation = animation;
        }
    }
    public void SetStat(string stat, float value, bool mainHand = true)
    {
        if (mainHand)
        {
            _currentMainHandPlayerStats.Add(stat);
        }
        else
        {
            _currentOffHandPlayerStats.Add(stat);
        }
        
        _player.Stats.Set(_statCategory, stat, value);
    }

    private readonly bool _clientSide = false;
    private readonly ICoreAPI _api;
    private readonly ICoreClientAPI? _clientApi;
    private readonly EntityPlayer _player;
    private PlayerAnimationData _currentMainHandAnimation = new();
    private PlayerAnimationData _currentOffHandAnimation = new();
    private readonly HashSet<string> _currentMainHandPlayerStats = new();
    private readonly HashSet<string> _currentOffHandPlayerStats = new();
    private readonly IAnimationManagerSystem _animationSystem;
    private const string _statCategory = "melee-weapon-player-behavior";
    
    private int _currentMainHandItemId = -1;
    private int _currentOffHandItemId = -1;
    private long _idleTimer = -1;

    private bool CheckIfItemsInHandsChanged()
    {
        int mainHandId = _player.ActiveHandItemSlot.Itemstack?.Id ?? -1;
        int offHandId = _player.LeftHandItemSlot.Itemstack?.Id ?? -1;
        bool anyChanged = mainHandId != _currentMainHandItemId || offHandId != _currentOffHandItemId;

        if (anyChanged && _currentMainHandItemId != mainHandId)
        {
            ProcessMainHandItemChanged();
            _currentMainHandItemId = mainHandId;
        }

        if (anyChanged && _currentOffHandItemId != offHandId)
        {
            ProcessOffHandItemChanged();
            _currentOffHandItemId = offHandId;
        }

        return !anyChanged;
    }
    private void ProcessMainHandItemChanged()
    {
        _currentMainHandAnimation.Stop(entity, _animationSystem, AnimationsEaseOutTime);

        foreach (string stat in _currentMainHandPlayerStats)
        {
            _player.Stats.Remove(_statCategory, stat);
        }

        ItemStack? stack = _player.ActiveHandItemSlot.Itemstack;

        if (stack == null || stack.Item is not MeleeWeaponItem weapon) return;

        weapon.ReadyAnimation.Start(entity, _animationSystem, AnimationsEaseOutTime);

        ResetIdleTimer();
    }
    private void ProcessOffHandItemChanged()
    {
        _currentOffHandAnimation.Stop(entity, _animationSystem, AnimationsEaseOutTime);

        foreach (string stat in _currentOffHandPlayerStats)
        {
            _player.Stats.Remove(_statCategory, stat);
        }

        ItemStack? stack = _player.ActiveHandItemSlot.Itemstack;

        if (stack == null || stack.Item is not MeleeWeaponItem weapon) return;

        weapon.ReadyAnimationOffhand.Start(entity, _animationSystem, AnimationsEaseOutTime);

        ResetIdleTimer();
    }
    private void ResetIdleTimer(TimeSpan? idleAnimationDelay = null)
    {
        if (_idleTimer != -1) _api.World.UnregisterCallback(_idleTimer);
        if (idleAnimationDelay == TimeSpan.Zero)
        {
            _idleTimer = -1;
        }
        else
        {
            _idleTimer = _api.World.RegisterCallback((dt) => PlayIdleAnimation(), (int)(idleAnimationDelay ?? DefaultIdleAnimationDelay).TotalMilliseconds);
        }
    }
    private void PlayIdleAnimation()
    {
        _idleTimer = -1;

        ItemStack? stackMainHand = _player.ActiveHandItemSlot.Itemstack;
        if (stackMainHand != null && stackMainHand.Item is MeleeWeaponItem weaponMainHand)
        {
            weaponMainHand.IdleAnimation.Start(entity, _animationSystem, AnimationsEaseOutTime);
        }

        ItemStack? stackOffHand = _player.LeftHandItemSlot.Itemstack;
        if (stackOffHand != null && stackOffHand.Item is MeleeWeaponItem weaponOffHand)
        {
            weaponOffHand.IdleAnimationOffhand.Start(entity, _animationSystem, AnimationsEaseOutTime);
        }
    }
}