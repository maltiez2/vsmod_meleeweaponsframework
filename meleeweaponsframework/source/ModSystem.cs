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
        api.RegisterItemClass("meleeweaponsframework:generic-melee-weapon", typeof(GenericMeleeWeapon));
    }

    public override void StartClientSide(ICoreClientAPI api)
    {
        _meleeSystemClient = new(api);
        _actionListener = new(api);

        _cursorRenderer = new(api);
        _directionController = new(api, _cursorRenderer);
        api.Event.RegisterRenderer(_cursorRenderer, EnumRenderStage.Ortho);

        new Harmony("meleeweaponsframework").PatchAll();
    }

    public override void StartServerSide(ICoreServerAPI api)
    {
        _meleeSystemServer = new(api);
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

    private MeleeSystemClient? _meleeSystemClient;
    private ActionListener? _actionListener;
    private MeleeSystemServer? _meleeSystemServer;
    private DirectionCursorRenderer? _cursorRenderer;
    private AttackDirectionController? _directionController;
}
