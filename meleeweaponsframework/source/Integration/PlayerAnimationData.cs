using AnimationManagerLib.API;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace MeleeWeaponsFramework;

public interface IPlayerAnimationData
{
    void Start(Entity entity, IAnimationManagerSystem system, TimeSpan easeInTime);
    void Start(Entity entity, IAnimationManagerSystem system, params RunParameters[] parameters);
    void Stop(Entity entity, IAnimationManagerSystem system, TimeSpan easeOutTime);
}

public readonly struct PlayerAnimationData : IPlayerAnimationData
{
    public readonly AnimationId TpHands;
    public readonly AnimationId FpHands;
    public readonly AnimationId TpLegs;
    public readonly AnimationId FpLegs;

    public const float DefaultHandsCategoryWeight = 512f;
    public const float DefaultLegsCategoryWeight = 16f;

    /// <summary>
    /// Registers animations for player model and stores ids for future use.
    /// </summary>
    /// <param name="code">Prefix for animation code, animation should be named like '{code}-{fp/tp}-{hands/legs}'</param>
    /// <param name="system"></param>
    /// <param name="easeInFrame">Frame used in <see cref="Start(Entity, IAnimationManagerSystem, TimeSpan)"/> method for EaseIn animation that is used by <see cref="MeleeWeaponPlayerBehavior"/> for Idle and Ready animations.</param>
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

    /// <summary>
    /// Eases in animations on frame specified on construction.
    /// </summary>
    /// <param name="entity"></param>
    /// <param name="system"></param>
    /// <param name="easeInTime"></param>
    public void Start(Entity entity, IAnimationManagerSystem system, TimeSpan easeInTime)
    {
        RunParameters parameters = RunParameters.EaseIn(easeInTime, _frame, ProgressModifierType.Sin);

        Start(entity, system, parameters);
    }
    /// <summary>
    /// Runs animations with specified parameters.
    /// </summary>
    /// <param name="entity"></param>
    /// <param name="system"></param>
    /// <param name="parameters"></param>
    public void Start(Entity entity, IAnimationManagerSystem system, params RunParameters[] parameters)
    {
        system.Run(new(entity.EntityId, AnimationTargetType.EntityThirdPerson), new(TpHands, parameters), synchronize: true);
        system.Run(new(entity.EntityId, AnimationTargetType.EntityFirstPerson), new(FpHands, parameters), synchronize: false);
        system.Run(new(entity.EntityId, AnimationTargetType.EntityThirdPerson), new(TpLegs, parameters), synchronize: true);
        system.Run(new(entity.EntityId, AnimationTargetType.EntityFirstPerson), new(FpLegs, parameters), synchronize: false);
    }
    public void Start(Entity entity, IAnimationManagerSystem system, PlayerAnimationData followUp, RunParameters followUpParameters, params RunParameters[] parameters)
    {
        AnimationRequest followUpRequestTpHands = new(followUp.TpHands, followUpParameters);
        AnimationRequest followUpRequestFpHands = new(followUp.FpHands, followUpParameters);
        AnimationRequest followUpRequestTpLegs = new(followUp.TpLegs, followUpParameters);
        AnimationRequest followUpRequestFpLegs = new(followUp.FpLegs, followUpParameters);

        AnimationId idTpHands = TpHands;
        AnimationId idFpHands = FpHands;
        AnimationId idTpLegs = TpLegs;
        AnimationId idFpLegs = FpLegs;

        AnimationRequest[] requestsTpHands = parameters.Select(parameters => new AnimationRequest(idTpHands, parameters)).Append(followUpRequestTpHands).ToArray();
        AnimationRequest[] requestsFpHands = parameters.Select(parameters => new AnimationRequest(idFpHands, parameters)).Append(followUpRequestFpHands).ToArray();
        AnimationRequest[] requestsTpLegs = parameters.Select(parameters => new AnimationRequest(idTpLegs, parameters)).Append(followUpRequestTpLegs).ToArray();
        AnimationRequest[] requestsFpLegs = parameters.Select(parameters => new AnimationRequest(idFpLegs, parameters)).Append(followUpRequestFpLegs).ToArray();

        system.Run(new(entity.EntityId, AnimationTargetType.EntityThirdPerson), new(requestsTpHands), synchronize: true);
        system.Run(new(entity.EntityId, AnimationTargetType.EntityFirstPerson), new(requestsFpHands), synchronize: false);
        system.Run(new(entity.EntityId, AnimationTargetType.EntityThirdPerson), new(requestsTpLegs), synchronize: true);
        system.Run(new(entity.EntityId, AnimationTargetType.EntityFirstPerson), new(requestsFpLegs), synchronize: false);
    }
    /// <summary>
    /// Eases out animations.
    /// </summary>
    /// <param name="entity"></param>
    /// <param name="system"></param>
    /// <param name="easeOutTime"></param>
    public void Stop(Entity entity, IAnimationManagerSystem system, TimeSpan easeOutTime)
    {
        RunParameters parameters = RunParameters.EaseOut(easeOutTime, ProgressModifierType.Sin);

        system.Run(new(entity.EntityId, AnimationTargetType.EntityThirdPerson), new(TpHands, parameters), synchronize: true);
        system.Run(new(entity.EntityId, AnimationTargetType.EntityFirstPerson), new(FpHands, parameters), synchronize: false);
        system.Run(new(entity.EntityId, AnimationTargetType.EntityThirdPerson), new(TpLegs, parameters), synchronize: true);
        system.Run(new(entity.EntityId, AnimationTargetType.EntityFirstPerson), new(FpLegs, parameters), synchronize: false);
    }

    /// <summary>
    /// Returns animation data for animation with 'empty-{fp/tp}-{hands/legs}' code and '0' ease in frame.
    /// </summary>
    /// <param name="system"></param>
    /// <returns></returns>
    public static PlayerAnimationData Empty(IAnimationManagerSystem system) => new("meleeweaponsframework-empty", system);

    private readonly float _frame = 0f;
}

public readonly struct PlayerSimpleAnimationData : IPlayerAnimationData
{
    public readonly AnimationId Tp;
    public readonly AnimationId Fp;

    public const float DefaultCategoryWeight = 512f;

    /// <summary>
    /// Registers animations for player model and stores ids for future use.
    /// </summary>
    /// <param name="code"></param>
    /// <param name="system"></param>
    /// <param name="easeInFrame">Frame used in <see cref="Start(Entity, IAnimationManagerSystem, TimeSpan)"/> method for EaseIn animation that is used by <see cref="MeleeWeaponPlayerBehavior"/> for Idle and Ready animations.</param>
    public PlayerSimpleAnimationData(string code, IAnimationManagerSystem system, float easeInFrame = 0f)
    {
        string tpCode = $"{code}";
        string fpCode = $"{code}-fp";

        Tp = new("MeleeWeaponsFramework:TpHands", tpCode, EnumAnimationBlendMode.Average, DefaultCategoryWeight);
        Fp = new("MeleeWeaponsFramework:FpHands", fpCode, EnumAnimationBlendMode.Average, DefaultCategoryWeight);

        AnimationData tpData = AnimationData.Player(tpCode);
        AnimationData fpData = AnimationData.Player(fpCode);

        system.Register(Tp, tpData);
        system.Register(Fp, fpData);

        _frame = easeInFrame;
    }

    /// <summary>
    /// Eases in animations on frame specified on construction.
    /// </summary>
    /// <param name="entity"></param>
    /// <param name="system"></param>
    /// <param name="easeInTime"></param>
    public void Start(Entity entity, IAnimationManagerSystem system, TimeSpan easeInTime)
    {
        RunParameters parameters = RunParameters.EaseIn(easeInTime, _frame, ProgressModifierType.Sin);

        Start(entity, system, parameters);
    }
    /// <summary>
    /// Runs animations with specified parameters.
    /// </summary>
    /// <param name="entity"></param>
    /// <param name="system"></param>
    /// <param name="parameters"></param>
    public void Start(Entity entity, IAnimationManagerSystem system, params RunParameters[] parameters)
    {
        system.Run(new(entity.EntityId, AnimationTargetType.EntityThirdPerson), new(Tp, parameters), synchronize: true);
        system.Run(new(entity.EntityId, AnimationTargetType.EntityFirstPerson), new(Fp, parameters), synchronize: false);
    }

    public void Start(Entity entity, IAnimationManagerSystem system , PlayerSimpleAnimationData followUp, RunParameters followUpParameters, params RunParameters[] parameters)
    {
        AnimationRequest followUpRequestTp = new(followUp.Tp, followUpParameters);
        AnimationRequest followUpRequestFp = new(followUp.Fp, followUpParameters);

        AnimationId idTp = Tp;
        AnimationId idFp = Fp;

        AnimationRequest[] requestsTp = parameters.Select(parameters => new AnimationRequest(idTp, parameters)).Append(followUpRequestTp).ToArray();
        AnimationRequest[] requestsFp = parameters.Select(parameters => new AnimationRequest(idFp, parameters)).Append(followUpRequestFp).ToArray();

        system.Run(new(entity.EntityId, AnimationTargetType.EntityThirdPerson), new(requestsTp), synchronize: true);
        system.Run(new(entity.EntityId, AnimationTargetType.EntityFirstPerson), new(requestsFp), synchronize: false);
    }
    /// <summary>
    /// Eases out animations.
    /// </summary>
    /// <param name="entity"></param>
    /// <param name="system"></param>
    /// <param name="easeOutTime"></param>
    public void Stop(Entity entity, IAnimationManagerSystem system, TimeSpan easeOutTime)
    {
        RunParameters parameters = RunParameters.EaseOut(easeOutTime, ProgressModifierType.Sin);

        system.Run(new(entity.EntityId, AnimationTargetType.EntityThirdPerson), new(Tp, parameters), synchronize: true);
        system.Run(new(entity.EntityId, AnimationTargetType.EntityFirstPerson), new(Fp, parameters), synchronize: false);
    }

    /// <summary>
    /// Returns animation data for animation with 'empty{-fp}' code and '0' ease in frame.
    /// </summary>
    /// <param name="system"></param>
    /// <returns></returns>
    public static PlayerSimpleAnimationData Empty(IAnimationManagerSystem system) => new("meleeweaponsframework-empty", system);

    private readonly float _frame = 0f;
}
