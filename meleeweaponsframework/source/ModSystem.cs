using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace MeleeWeaponsFramework;

public class MeleeWeaponsFramework : ModSystem
{
    public override void StartClientSide(ICoreClientAPI api)
    {
        _meleeSystemClient = new(api);
    }

    public override void StartServerSide(ICoreServerAPI api)
    {
        _meleeSystemServer = new(api);
    }

    private MeleeSystemClient? _meleeSystemClient;
    private MeleeSystemServer? _meleeSystemServer;
}
