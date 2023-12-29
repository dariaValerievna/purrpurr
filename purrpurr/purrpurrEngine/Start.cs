using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;
using System.Security.Cryptography.X509Certificates;
using System.Net;
using System.Security.Permissions;
using System.Security.Cryptography;
using Autodesk.Revit.UI.Events;
using System.Diagnostics;

namespace purrpurrPlugin
{
    [TransactionAttribute(TransactionMode.Manual)]
    [RegenerationAttribute(RegenerationOption.Manual)]
    public class Start : IExternalCommand
    {
        private static ExternalCommandData CommandData { get; set; }
        private static ElementSet Elements { get; set; }
        public class PluginEngine
        {
            private static string debug;
            public int RoomCount { get; }
            public bool IsSeparateBathrooms { get; }
            public bool IsWardrobe { get; }
            public bool IsCombinedKitchen { get; }
            public bool IsLoggia { get; }
            public int HouseClass { get; }
            public PluginEngine(int RC, bool ISB, bool IW, bool ICK, bool IL, int HC)
            {
                RoomCount = RC;
                IsSeparateBathrooms = ISB;
                IsWardrobe = IW;
                IsCombinedKitchen = ICK;
                IsLoggia = IL;
                HouseClass = HC;
            }

            private static GeometryInstance GetGeometryInstance(GeometryElement geometryElement)
            {
                foreach (GeometryObject element in geometryElement)
                    if (element is GeometryInstance geometryInstance)
                        return geometryInstance;
                return null;
            }

            private static Solid GetSolid(GeometryElement geometryElement)
            {
                foreach (GeometryObject element in geometryElement)
                    if (element is Solid solid)
                        if (solid.Volume > 0)
                            return solid;
                return null;
            }

            private static List<LineSegment> GetBottomEdges(Solid solid)
            {
                List<Edge> allBottomEdges = new List<Edge>();
                List<Edge> outBottomEdges = new List<Edge>();
                List<Edge> inBottomEdges = new List<Edge>();
                EdgeArray edges = solid.Edges;
                double minZ = double.MaxValue;
                double maxZ = double.MinValue;
                foreach (Edge edge in edges)
                {
                    if (edge.Evaluate(0).Z < minZ)
                        minZ = edge.Evaluate(0).Z;
                    if(edge.Evaluate(1).Z < minZ)
                        minZ = edge.Evaluate(1).Z;
                    if (edge.Evaluate(0).Z > maxZ)
                        maxZ = edge.Evaluate(0).Z;
                    if (edge.Evaluate(1).Z > maxZ)
                        maxZ = edge.Evaluate(1).Z;
                }
                foreach (Edge edge in edges)
                    if(edge.Evaluate(0).Z == maxZ && edge.Evaluate(1).Z == maxZ)
                        allBottomEdges.Add(edge);
                Edge maxSizeEdge = allBottomEdges[0];
                foreach (Edge edge in allBottomEdges)
                    if (edge.ApproximateLength > maxSizeEdge.ApproximateLength)
                        maxSizeEdge = edge;
                outBottomEdges.Add(maxSizeEdge);
                for (int i = 0; i < (allBottomEdges.Count/2)-1; i++)
                    foreach (Edge edge in allBottomEdges)
                        if (outBottomEdges[i].Evaluate(1).IsAlmostEqualTo(edge.Evaluate(0)))
                            outBottomEdges.Add(edge);
                inBottomEdges = allBottomEdges.Except(outBottomEdges).ToList();
                List<LineSegment> result = new List<LineSegment>();
                foreach (Edge edge in inBottomEdges)
                    result.Add(new LineSegment(new XYZ(edge.Evaluate(0).X, edge.Evaluate(0).Y, minZ), new XYZ(edge.Evaluate(1).X, edge.Evaluate(1).Y, minZ)));
                return result;
            }
            public List<VirtualWall> GetVirtualWalls(List<LineSegment> edges, List<FamilyInstance> doors, List<FamilyInstance> windows)
            {
                List<VirtualWall> result = new List<VirtualWall>();
                List<BoundingBoxXYZ> doorsBB = new List<BoundingBoxXYZ>();
                List<BoundingBoxXYZ> windowsBB = new List<BoundingBoxXYZ>();
                foreach (FamilyInstance door in doors)
                    doorsBB.Add(door.get_Geometry(new Options()).GetBoundingBox());
                foreach (FamilyInstance window in windows)
                    windowsBB.Add(window.get_Geometry(new Options()).GetBoundingBox());
                foreach (LineSegment edge in edges)
                {
                    VirtualWall current = new VirtualWall(edge);
                    foreach (BoundingBoxXYZ door in doorsBB)
                    { 
                        List<XYZ> intersections = new List<XYZ>();
                        List<LineSegment> BBEdges = new List<LineSegment>
                        {
                            new LineSegment(new XYZ(door.Min.X, door.Min.Y, 0), new XYZ(door.Min.X, door.Max.Y, 0)),
                            new LineSegment(new XYZ(door.Min.X, door.Max.Y, 0), new XYZ(door.Max.X, door.Max.Y, 0)),
                            new LineSegment(new XYZ(door.Max.X, door.Max.Y, 0), new XYZ(door.Max.X, door.Min.Y, 0)),
                            new LineSegment(new XYZ(door.Max.X, door.Min.Y, 0), new XYZ(door.Min.X, door.Min.Y, 0))
                        };
                        foreach (LineSegment BBEdge in BBEdges)
                        {
                            XYZ intersection = edge.GetIntersection(BBEdge);
                            if (intersection != null)
                                intersections.Add(intersection);
                        }
                        if(intersections.Count == 2)
                            current.Doors.Add(new LineSegment(intersections[0], intersections[1]));
                    }
                    foreach (BoundingBoxXYZ window in windowsBB)
                    {
                        List<XYZ> intersections = new List<XYZ>();
                        List<LineSegment> BBEdges = new List<LineSegment>
                        {
                            new LineSegment(new XYZ(window.Min.X, window.Min.Y, 0), new XYZ(window.Min.X, window.Max.Y, 0)),
                            new LineSegment(new XYZ(window.Min.X, window.Max.Y, 0), new XYZ(window.Max.X, window.Max.Y, 0)),
                            new LineSegment(new XYZ(window.Max.X, window.Max.Y, 0), new XYZ(window.Max.X, window.Min.Y, 0)),
                            new LineSegment(new XYZ(window.Max.X, window.Min.Y, 0), new XYZ(window.Min.X, window.Min.Y, 0))
                        };
                        foreach (LineSegment BBEdge in BBEdges)
                        {
                            XYZ intersection = edge.GetIntersection(BBEdge);
                            if (intersection != null)
                                intersections.Add(intersection);
                        }
                        if (intersections.Count == 2)
                            current.Doors.Add(new LineSegment(intersections[0], intersections[1]));
                    }
                    result.Add(current);
                }
                return result;
            }

            List<VirtualWall> GetFIAsWalls(FamilyInstance fi, Document doc)
            {
                List<VirtualWall> walls = new List<VirtualWall>();
                GeometryElement geometry = fi.get_Geometry(new Options());
                BoundingBoxXYZ boundingBox = geometry.GetBoundingBox();
                GeometryInstance geometryInstance = GetGeometryInstance(geometry);
                GeometryElement geometryElement = geometryInstance.GetInstanceGeometry();
                Solid solid = GetSolid(geometryElement);
                List<LineSegment> edges = GetBottomEdges(solid);
                List<FamilyInstance> doors = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Doors).OfClass(typeof(FamilyInstance)).Cast<FamilyInstance>().ToList();
                List<FamilyInstance> windows = new FilteredElementCollector(doc).OfClass(typeof(FamilyInstance)).OfCategory(BuiltInCategory.OST_Windows).Cast<FamilyInstance>().ToList();
                walls = GetVirtualWalls(edges, doors, windows);
                return walls;
            }

            public List<VirtualWall> GetWalls(Document doc)
            {
                FilteredElementCollector wallFilter = new FilteredElementCollector(doc);
                List<FamilyInstance> wallsFI = wallFilter.OfCategory(BuiltInCategory.OST_Walls).OfClass(typeof(FamilyInstance)).Cast<FamilyInstance>().ToList();
                List<VirtualWall> walls = new List<VirtualWall>();
                foreach (FamilyInstance fi in wallsFI)
                    foreach (VirtualWall wall in GetFIAsWalls(fi, doc))
                        walls.Add(wall);
                List<VirtualWall> debuggingList = walls.Where(x => x.Doors == null).ToList();
                debug += debuggingList.Count;
                TaskDialog.Show("Revit", debug);
                return walls;
            }

            public void Run()
            {
                List<VirtualWall> walls = GetWalls(Start.CommandData.Application.ActiveUIDocument.Document);
            }
        }
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            CommandData = commandData;
            Elements = elements;
            UserWindowControl userWindowControl = new UserWindowControl();
            userWindowControl.Show();
            return Result.Succeeded;
        }
    }

    public class LineSegment
    {
        public XYZ Start { get; set; }
        public XYZ End { get; set; }
        public LineSegment(XYZ start, XYZ end)
        {
            Start = start;
            End = end;
        }
        public XYZ GetIntersection(LineSegment line)
        {
            double x1 = Start.X;
            double y1 = Start.Y;
            double x2 = End.X;
            double y2 = End.Y;
            double x3 = line.Start.X;
            double y3 = line.Start.Y;
            double x4 = line.End.X;
            double y4 = line.End.Y;
            double k1 = (x2 - x1) / (y2 - y1);
            double k2 = (x4 - x3) / (y4 - y3);
            if(k1 == k2)
                return null;
            double x = ((x1 * y2 - x2 * y1) * (x4 - x3) - (x3 * y4 - x4 * y3) * (x2 - x1)) / ((y1 - y2) * (x4 - x3) - (y3 - y4) * (x2 - x1));
            double y = ((y3 - y4) * x - (x3 * y4 - x4 * y3)) / (x4 - x3);
            if (((x1 <= x) && (x2 >= x) && (x3 <= x) && (x4 >= x)) || ((y1 <= y) && (y2 >= y) && (y3 <= y) && (y4 >= y)))
                return new XYZ(x, y, 0);
            return null;
        }
    }

    public class VirtualWall
    {
        public LineSegment Line { get; set; }
        public List<LineSegment> Windows { get; set; } = new List<LineSegment>();
        public List<LineSegment> Doors { get; set; } = new List<LineSegment>();
        public VirtualWall(LineSegment line)
        {
            Line = line;
        }
    }

    public class Room
    {
        public List<VirtualWall> Walls { get; set; } 
        public bool HasWindow { get; set; }
        public bool HasDoor { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public Room(List<VirtualWall> walls, bool hasWindow, bool hasDoor, double width, double height)
        {
            Walls = walls;
            HasWindow = hasWindow;
            HasDoor = hasDoor;
            Width = width;
            Height = height;
        }
    }

    public class Kitchen : Room
    {
        public Kitchen(List<VirtualWall> walls, double w, double h): base(walls, false, false, w , h) { }
    }

    public class Toliet : Room
    {
        public Toliet(List<VirtualWall> walls, double w, double h) : base(walls, false, false, w, h) { }
    }

    public class Wardrobe : Room
    {
        public Wardrobe(List<VirtualWall> walls, double w, double h) : base(walls, false, false, w, h) { }
    }

    public class Hall : Room
    {
        public Hall(List<VirtualWall> walls, double w, double h) : base(walls, false, true, w, h) { }
    }

    public class LivingRoom : Room 
    {
        public LivingRoom(List<VirtualWall> walls, double w, double h) : base(walls, true, false, w, h) { }
    }

    public class Loggia : Room
    {
        public Loggia(List<VirtualWall> walls, double w, double h) : base(walls, true, false, w, h) { }
    }

   
}
