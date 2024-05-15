using System.Numerics;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace MeleeWeaponsFramework;

public interface ICollider
{
    void Render(ICoreClientAPI api, EntityPlayer entityPlayer, int color = ColorUtil.WhiteArgb);
    ICollider? Transform(EntityPlayer entity, ItemSlot itemSlot, ICoreClientAPI api, bool right = true);
    ICollider Transform(Matrixf modelMatrix, EntityPos playerPos);
}

public interface IHasCollider
{
    ICollider RelativeCollider { get; }
    ICollider InWorldCollider { get; set; }
}




public interface IHasLineCollider
{
    LineSegmentCollider RelativeCollider { get; }
    LineSegmentCollider InWorldCollider { get; set; }
}

public interface IHasRectangularCollider
{
    RectangularCollider RelativeCollider { get; }
    RectangularCollider InWorldCollider { get; set; }
}

public interface IHasOctagonalCollider
{
    OctagonalCollider RelativeCollider { get; }
    OctagonalCollider InWorldCollider { get; set; }
}

public interface IWeaponCollider : ICollider
{
    bool RoughIntersect(Cuboidf collisionBox);
    Vector3? IntersectCuboids(IEnumerable<Cuboidf> collisionBoxes);
    Vector3? IntersectCuboid(Cuboidf collisionBox);
    (Block, Vector3)? IntersectTerrain(ICoreClientAPI api);
}

public interface IParryCollider : ICollider
{
    public bool IntersectSegment(LineSegmentCollider segment, out float parameter, out Vector3 intersection);
}