using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace MeleeWeaponsFramework;

public class MeleeWeaponsFramework : ModSystem
{
    public override void StartClientSide(ICoreClientAPI api)
    {
        _meleeSystemClient = new(api);
        _actionListener = new(api);

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

    private MeleeSystemClient? _meleeSystemClient;
    private ActionListener? _actionListener;
    private MeleeSystemServer? _meleeSystemServer;
}
