using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace MeleeWeaponsFramework;

public class MeleeWeaponsFrameworkModSystem : ModSystem
{
    public override void Start(ICoreAPI api)
    {
        api.RegisterEntityBehaviorClass("meleeweaponsframework:meleeweapon", typeof(MeleeWeaponPlayerBehavior));
        api.RegisterEntityBehaviorClass("meleeweaponsframework:meleeblock", typeof(MeleeBlockPlayerBehavior));
        api.RegisterItemClass("meleeweaponsframework:generic-melee-weapon", typeof(GenericMeleeWeapon));
        api.RegisterItemClass("meleeweaponsframework:vanilla-spear-melee-weapon", typeof(VanillaSpear)); 
    }

    public override void StartClientSide(ICoreClientAPI api)
    {
        _meleeSystemClient = new(api);
        _throwSystemClient = new(api);
        _actionListener = new(api);
        _hackingSystemClient = new(api);
        _meleeBlockSystemClient = new(api);

        _cursorRenderer = new(api);
        _directionController = new(api, _cursorRenderer);
        api.Event.RegisterRenderer(_cursorRenderer, EnumRenderStage.Ortho);

        new Harmony("meleeweaponsframework").PatchAll();
    }

    public override void StartServerSide(ICoreServerAPI api)
    {
        _meleeSystemServer = new(api);
        _throwSystemServer = new(api);
        _hackingSystemServer = new(api);
        _meleeBlockSystemServer = new(api);
    }

    public override void Dispose()
    {
        new Harmony("meleeweaponsframework").UnpatchAll();
    }

    internal MeleeSystemClient? MeleeSystemClient => _meleeSystemClient;
    internal ActionListener? ActionListener => _actionListener;
    internal MeleeSystemServer? MeleeSystemServer => _meleeSystemServer;
    internal DirectionCursorRenderer? CursorRenderer => _cursorRenderer;
    internal AttackDirectionController? DirectionController => _directionController;
    internal ThrowSystemClient? ThrowSystemClient => _throwSystemClient;
    internal ThrowSystemServer? ThrowSystemServer => _throwSystemServer;
    internal HackingSystemClient? HackingSystemClient => _hackingSystemClient;
    internal HackingSystemServer? HackingSystemServer => _hackingSystemServer;
    internal MeleeBlockSystemClient? BlockSystemClient => _meleeBlockSystemClient;
    internal MeleeBlockSystemServer? BlockSystemServer => _meleeBlockSystemServer;

    private MeleeSystemClient? _meleeSystemClient;
    private ActionListener? _actionListener;
    private MeleeSystemServer? _meleeSystemServer;
    private DirectionCursorRenderer? _cursorRenderer;
    private AttackDirectionController? _directionController;
    private ThrowSystemClient? _throwSystemClient;
    private ThrowSystemServer? _throwSystemServer;
    private HackingSystemClient? _hackingSystemClient;
    private HackingSystemServer? _hackingSystemServer;
    private MeleeBlockSystemClient? _meleeBlockSystemClient;
    private MeleeBlockSystemServer? _meleeBlockSystemServer;
}
