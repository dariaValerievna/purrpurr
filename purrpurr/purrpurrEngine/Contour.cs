using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace purrpurrPlugin
{
    public class ContourFlat
    {
        public IGeometricShape GeometricShape { get; }
        public Side SideWithDoor { get; set; }
        public Side SideWithWindow { get; set; }
        public string Name { get; }

        public ContourFlat(IGeometricShape geometricShape, Side sideWithDoor, Side sideWithWindow, string name = "")
        {
            GeometricShape = geometricShape;
            SideWithDoor = sideWithDoor;
            SideWithWindow = sideWithWindow;
            Name = name;
        }

        public override string ToString() => Name;
    }

}
