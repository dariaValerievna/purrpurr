using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;
using Microsoft.SqlServer.Server;


namespace purrpurrPlugin
{
    [TransactionAttribute(TransactionMode.Manual)]
    [RegenerationAttribute(RegenerationOption.Manual)]
    public class Start : IExternalCommand
    {
        private static ExternalCommandData CommandData { get; set; }
        public class PluginEngine
        {
            public int RoomCount { get; }
            public bool IsSeparateBathrooms { get; }
            public bool IsWardrobe { get; }
            public bool IsCombinedKitchen { get; }
            public bool IsLoggia { get; }
            public int HouseClass { get; }
            private const double WidthInnerWall = 0.49;

            public PluginEngine(int RC, bool ISB, bool IW, bool ICK, bool IL, int HC)
            {
                RoomCount = RC;
                IsSeparateBathrooms = ISB;
                IsWardrobe = IW;
                IsCombinedKitchen = ICK;
                IsLoggia = IL;
                HouseClass = HC;
            }

            private static Solid GetSolid(GeometryElement geometryElement)
            {
                foreach (GeometryObject element in geometryElement)
                    if (element is Solid solid)
                        if (solid.Volume > 0)
                            return solid;
                return null;
            }

            private static Rectangle GetInnerBottomShape(Solid solid)
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
                    if (edge.Evaluate(1).Z < minZ)
                        minZ = edge.Evaluate(1).Z;
                    if (edge.Evaluate(0).Z > maxZ)
                        maxZ = edge.Evaluate(0).Z;
                    if (edge.Evaluate(1).Z > maxZ)
                        maxZ = edge.Evaluate(1).Z;
                }
                foreach (Edge edge in edges)
                    if (edge.Evaluate(0).Z == maxZ && edge.Evaluate(1).Z == maxZ)
                        allBottomEdges.Add(edge);
                Edge maxSizeEdge = allBottomEdges[0];
                foreach (Edge edge in allBottomEdges)
                    if (edge.ApproximateLength > maxSizeEdge.ApproximateLength)
                        maxSizeEdge = edge;
                outBottomEdges.Add(maxSizeEdge);
                for (int i = 0; i < (allBottomEdges.Count / 2) - 1; i++)
                    foreach (Edge edge in allBottomEdges)
                        if (outBottomEdges[i].Evaluate(1).IsAlmostEqualTo(edge.Evaluate(0)))
                            outBottomEdges.Add(edge);
                inBottomEdges = allBottomEdges.Except(outBottomEdges).ToList();
                List<XYZ> points = new List<XYZ>();
                foreach (Edge edge in inBottomEdges)
                    points.Add(new XYZ(edge.Evaluate(0).X, edge.Evaluate(0).Y, 0));
                (double min, double max) X = new ValueTuple<double, double>();
                X.min = double.MaxValue;
                X.max = double.MinValue;
                (double min, double max) Y = new ValueTuple<double, double>();
                Y.min = double.MaxValue;
                Y.max = double.MinValue;
                foreach (XYZ point in points)
                {
                    if (point.X <= X.min)
                        X.min = point.X;
                    if(point.Y <= Y.min)
                        Y.min = point.Y;
                    if(point.X >= X.max)
                        X.max = point.X;
                    if(point.Y >= Y.max)
                        Y.max = point.Y;
                }
                return new Rectangle(new XYZ[] 
                {
                    new XYZ(X.min, Y.min, 0),
                    new XYZ(X.max, Y.min, 0),
                    new XYZ(X.max, Y.max, 0),
                    new XYZ(X.min, Y.max, 0)
                });
            }

            /// <summary>
            /// Преобразует IGeometricShape в ContourFlat с указанием дверей и окон
            /// </summary>
            /// <param name="edges"></param>
            /// <param name="doors"></param>
            /// <param name="windows"></param>
            /// <returns></returns>
            private ContourFlat SetDoorsWindows(IGeometricShape edges, List<FamilyInstance> doors, List<FamilyInstance> windows)
            {
                List<BoundingBoxXYZ> doorsBB = new List<BoundingBoxXYZ>();
                List<BoundingBoxXYZ> windowsBB = new List<BoundingBoxXYZ>();
                foreach (FamilyInstance door in doors)
                    doorsBB.Add(door.get_Geometry(new Options()).GetBoundingBox());
                foreach (FamilyInstance window in windows)
                    windowsBB.Add(window.get_Geometry(new Options()).GetBoundingBox());
                ContourFlat result = new ContourFlat(edges, null, null);
                foreach (Side edge in edges.Sides)
                {
                    foreach (BoundingBoxXYZ door in doorsBB)
                    {
                        List<XYZ> intersections = new List<XYZ>();
                        List<Side> BBEdges = new List<Side>
                        {
                            new Side(new XYZ(door.Min.X, door.Min.Y, 0), new XYZ(door.Min.X, door.Max.Y, 0)),
                            new Side(new XYZ(door.Min.X, door.Max.Y, 0), new XYZ(door.Max.X, door.Max.Y, 0)),
                            new Side(new XYZ(door.Max.X, door.Max.Y, 0), new XYZ(door.Max.X, door.Min.Y, 0)),
                            new Side(new XYZ(door.Max.X, door.Min.Y, 0), new XYZ(door.Min.X, door.Min.Y, 0))
                        };
                        foreach (Side BBEdge in BBEdges)
                        {
                            XYZ intersection = edge.GetIntersection(BBEdge);
                            if (intersection != null)
                                intersections.Add(intersection);
                        }
                        if (intersections.Count == 2)
                            result.SideWithDoor = edge;
                    }
                    foreach (BoundingBoxXYZ window in windowsBB)
                    {
                        List<XYZ> intersections = new List<XYZ>();
                        List<Side> BBEdges = new List<Side>
                        {
                            new Side(new XYZ(window.Min.X, window.Min.Y, 0), new XYZ(window.Min.X, window.Max.Y, 0)),
                            new Side(new XYZ(window.Min.X, window.Max.Y, 0), new XYZ(window.Max.X, window.Max.Y, 0)),
                            new Side(new XYZ(window.Max.X, window.Max.Y, 0), new XYZ(window.Max.X, window.Min.Y, 0)),
                            new Side(new XYZ(window.Max.X, window.Min.Y, 0), new XYZ(window.Min.X, window.Min.Y, 0))
                        };
                        foreach (Side BBEdge in BBEdges)
                        {
                            XYZ intersection = edge.GetIntersection(BBEdge);
                            if (intersection != null)
                            {
                                intersections.Add(intersection);
                            }
                        }
                        if (intersections.Count == 2)
                            result.SideWithWindow = edge;
                    }
                }
                return result;
            }

            /// <summary>
            /// Возвращает контур квартиры из указанного документа
            /// </summary>
            /// <param name="doc"></param>
            /// <returns></returns>
            private ContourFlat GetContourFlat(Document doc)
            {
                FilteredElementCollector wallFilter = new FilteredElementCollector(doc);
                List<FamilyInstance> wallsFI = wallFilter.OfCategory(BuiltInCategory.OST_Walls).OfClass(typeof(FamilyInstance)).Cast<FamilyInstance>().ToList();
                List<FamilyInstance> doors = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Doors).OfClass(typeof(FamilyInstance)).Cast<FamilyInstance>().ToList();
                List<FamilyInstance> windows = new FilteredElementCollector(doc).OfClass(typeof(FamilyInstance)).OfCategory(BuiltInCategory.OST_Windows).Cast<FamilyInstance>().ToList();
                FamilyInstance fi = wallsFI[0]; 
                GeometryElement geometry = fi.get_Geometry(new Options()).GetTransformed(fi.GetTransform());
                Solid solid = GetSolid(geometry);
                Rectangle bottomShape = GetInnerBottomShape(solid);
                ContourFlat result = SetDoorsWindows(bottomShape, doors, windows);
                return result;
            }

            /// <summary>
            /// Возвращает List комнат для текущих глобальных параметров
            /// </summary>
            /// <returns></returns>
            private List<Room> CreateRooms()
            {
                List<Room> result = new List<Room>();
                for (int i = 0; i < RoomCount; i++)
                    result.Add(new LivingRoom());
                result.Add(new Bathroom());
                result.Add(new Kitchen());
                result.Add(new Hallway());
                return result;
            }

            /// <summary>
            /// Запускает генерацию, возвращает List квартир
            /// </summary>
            /// <param name="rooms"></param>
            /// <param name="contourFlat"></param>
            /// <returns></returns>
            private static List<List<Room>> Generate(List<Room> rooms, ContourFlat contourFlat)
            {
                List<Room> sortedRooms = rooms.OrderByDescending(x => x.SquareMeter).ToList();
                Rectangle contourRectangle = (Rectangle)contourFlat.GeometricShape;
                List<List<Room>> variants = GetVariantsFlats(sortedRooms, contourFlat);
                List<List<Room>> result = new List<List<Room>>();

                for (int i = 0; i < variants.Count; i++)
                {
                    for (int j = 0; j < variants[i].Count; j++)
                    {
                        ProcessRoomsDistanceBorders(variants[i], j, contourRectangle);
                        ProcessSizesPlacedRooms(variants[i], j, contourRectangle);
                    }

                    if (CheckCompilance(result, variants[i], contourFlat))
                        result.Add(variants[i]);
                }

                TaskDialog.Show("Check Variants", $"{variants.Count}");
                return result;
            }

            private static bool CheckCompilance(List<List<Room>> flatVariants, List<Room> rooms, ContourFlat contourFlat)
            {
                if (rooms.Any(r => r == null) ||
                    !rooms.All(r => r.IsCorectPositionRelativeWalls(contourFlat.SideWithDoor, contourFlat.SideWithWindow)))
                    return false;

                bool previousSimilar = true;

                for (int i = flatVariants.Count - 1; i >= 0; i--)
                {
                    for (int j = 0; j < flatVariants[i].Count; j++)
                    {
                        if (rooms[j] != null && flatVariants[i][j].Name == rooms[j].Name)
                        {
                            if (flatVariants[i][j].Rectangle.Contains(rooms[j].Rectangle))
                                previousSimilar = true;
                            else
                                previousSimilar = false;
                        }
                    }

                    if (previousSimilar) return false;
                }

                return true;
            }

            private static List<List<Room>> GetVariantsFlats(List<Room> rooms, ContourFlat contourFlat)
            {
                List<List<Room>> result = new List<List<Room>>();

                for (int i = 0; i < rooms.Count; i++)
                {
                    if (result.Count == 0)
                    {
                        FillRoom(result, new List<Room>(), rooms[i], contourFlat, rooms.Count(r => r.Name == rooms[i].Name));
                        continue;
                    }
                    for (int j = 0; j < result.Count; j++)
                        FillRoom(result, result[j], rooms[i], contourFlat, rooms.Count(r => r.Name == rooms[i].Name));

                    result = result.Where(r => r.Count == i + 1).ToList();
                }

                return result.Where(v => v.Count >= rooms.Count - 1).ToList();
            }

            private static void FillRoom(List<List<Room>> roomVariants, List<Room> workVariant, Room room, ContourFlat contourFlat,
                int countRoomInVariant)
            {
                Rectangle contourRectangle = (Rectangle)contourFlat.GeometricShape;
                List<XYZ> possiblePosition = GetPossiblePosition(workVariant, contourRectangle, room);

                foreach (XYZ position in possiblePosition)
                {
                    Room newRoom = room.CreateNew(new Rectangle(position, room.WidthFeet, room.HeightFeet));

                    if (contourRectangle.Contains(newRoom.Rectangle))
                    {
                        newRoom = ProcessingIntersections(newRoom, workVariant);
                        if (newRoom != null && workVariant.Count(r => r.Name == newRoom.Name) < countRoomInVariant)
                        {
                            List<Room> newVariant = new List<Room>();
                            newVariant.AddRange(workVariant.ToArray());
                            newVariant.Add(newRoom);
                            roomVariants.Add(newVariant);
                        }
                    }
                }
            }

            private static List<XYZ> GetPossiblePosition(List<Room> rooms, Rectangle contourRectangle, Room room)
            {
                List<XYZ> result = new List<XYZ>();
                result.AddRange(GetPossiblePositionsContour(room, contourRectangle));

                for (int i = 0; i < rooms.Count; i++)
                {
                    result.AddRange(GetPossiblePositionsRoom(rooms[i].Rectangle, room));
                }

                return result;
            }

            private static List<XYZ> GetPossiblePositionsContour(Room room, Rectangle contourRectangle)
            {
                return new List<XYZ>()
            {
                contourRectangle.MinXminY,
                contourRectangle.MaxXminY - new XYZ(room.WidthFeet, 0, 0),
                new XYZ(contourRectangle.MaxXmaxY.X - room.WidthFeet, contourRectangle.MaxXmaxY.Y - room.HeightFeet, contourRectangle.MinXminY.Z),
                contourRectangle.MinXmaxY - new XYZ(0, room.WidthFeet, 0),
            };

            }

            private static List<XYZ> GetPossiblePositionsRoom(Rectangle rectangleInContur, Room room)
            {
                HashSet<XYZ> result = new HashSet<XYZ>();

                List<XYZ> newPoints = new List<XYZ>()
                {
                rectangleInContur.MinXminY + new XYZ(-WidthInnerWall, -WidthInnerWall, 0),
                rectangleInContur.MinXminY + new XYZ(-WidthInnerWall, 0, 0),
                rectangleInContur.MinXminY + new XYZ(0, -WidthInnerWall, 0),
                rectangleInContur.MaxXminY + new XYZ(WidthInnerWall, -WidthInnerWall, 0),
                rectangleInContur.MaxXminY + new XYZ(WidthInnerWall, 0, 0),
                rectangleInContur.MaxXminY + new XYZ(0, -WidthInnerWall, 0),
                rectangleInContur.MaxXmaxY + new XYZ(WidthInnerWall, WidthInnerWall, 0),
                rectangleInContur.MaxXmaxY + new XYZ(WidthInnerWall, 0, 0),
                rectangleInContur.MaxXmaxY + new XYZ(0, WidthInnerWall, 0),
                rectangleInContur.MinXmaxY + new XYZ(-WidthInnerWall, WidthInnerWall, 0),
                rectangleInContur.MinXmaxY + new XYZ(0, WidthInnerWall, 0),
                rectangleInContur.MinXmaxY + new XYZ(-WidthInnerWall, 0, 0)
                };

                foreach (XYZ point in newPoints)
                    for (double i = -room.WidthFeet; i <= room.WidthFeet; i += room.WidthFeet) 
                        for (double j = -room.HeightFeet; j <= room.HeightFeet; j += room.HeightFeet)
                        {
                            XYZ newPosition1 = point + new XYZ(i, j, 0);
                            if (!result.Contains(newPosition1))
                                result.Add(newPosition1);
                        }

                return result.ToList();
            }

            private static bool CheckIntersections(Room room, List<Room> spacedRooms)
            {
                List<Room> intersectionRooms = spacedRooms.Where(m => m.Rectangle.IntersectsWith(room.Rectangle)).ToList();

                if (intersectionRooms.Count == 0) return false;
                else if (intersectionRooms.Count == 1)
                {
                    if (intersectionRooms[0].Name == room.Name && (room.Rectangle.Contains(intersectionRooms[0].Rectangle) ||
                        intersectionRooms[0].Rectangle.Contains(room.Rectangle)))
                        return false;
                }

                return true;
            }

            private static Room ProcessingIntersections(Room room, List<Room> spacedRooms)
            {
                if (!CheckIntersections(room, spacedRooms))
                    return room;

                return null;
            }

            private static void ProcessRoomsDistanceBorders(List<Room> rooms, int index, Rectangle contourRectangle)
            {
                if (!rooms.Any(r => r == null) && rooms[index].Rectangle.MinXminY.X != contourRectangle.MinXminY.X)
                {
                    Rectangle rect = new Rectangle(new XYZ(contourRectangle.MinXminY.X, rooms[index].Rectangle.MinXminY.Y, rooms[index].Rectangle.MinXminY.Z),
                        rooms[index].Rectangle.MinXmaxY - new XYZ(0.1, 0, 0));

                    if (!CheckRoomsOnRectangle(rooms, rect))
                        rooms[index] = rooms[index].CreateNew(
                            new XYZ(contourRectangle.MinXminY.X, rooms[index].Rectangle.MinXminY.Y, rooms[index].Rectangle.MinXminY.Z),
                            rooms[index].Rectangle.MaxXmaxY);
                }
                if (!rooms.Any(r => r == null) && rooms[index].Rectangle.MaxXmaxY.X != contourRectangle.MaxXmaxY.X)
                {
                    Rectangle rect = new Rectangle(rooms[index].Rectangle.MaxXminY + new XYZ(0.1, 0, 0),
                        new XYZ(contourRectangle.MaxXmaxY.X, rooms[index].Rectangle.MaxXmaxY.Y, rooms[index].Rectangle.MaxXmaxY.Z));

                    if (!CheckRoomsOnRectangle(rooms, rect))
                        rooms[index] = rooms[index].CreateNew(rooms[index].Rectangle.MinXminY,
                            new XYZ(contourRectangle.MaxXmaxY.X, rooms[index].Rectangle.MaxXmaxY.Y, rooms[index].Rectangle.MaxXmaxY.Z));
                }
                if (!rooms.Any(r => r == null) && rooms[index].Rectangle.MinXminY.Y != contourRectangle.MinXminY.Y)
                {
                    Rectangle rect = new Rectangle(new XYZ(rooms[index].Rectangle.MinXminY.X, contourRectangle.MinXminY.Y, rooms[index].Rectangle.MaxXmaxY.Z),
                        rooms[index].Rectangle.MaxXminY - new XYZ(0, 0.1, 0));

                    if (!CheckRoomsOnRectangle(rooms, rect))
                        rooms[index] = rooms[index].CreateNew(new XYZ(rooms[index].Rectangle.MinXminY.X, contourRectangle.MinXminY.Y,
                            rooms[index].Rectangle.MinXminY.Z), rooms[index].Rectangle.MaxXmaxY);
                }
                if (!rooms.Any(r => r == null) && rooms[index].Rectangle.MaxXmaxY.Y != contourRectangle.MaxXmaxY.Y)
                {
                    Rectangle rect = new Rectangle(rooms[index].Rectangle.MinXmaxY + new XYZ(0, 0.1, 0),
                        new XYZ(rooms[index].Rectangle.MaxXmaxY.X, contourRectangle.MaxXmaxY.Y, rooms[index].Rectangle.MaxXmaxY.Z));

                    if (!CheckRoomsOnRectangle(rooms, rect))
                        rooms[index] = rooms[index].CreateNew(rooms[index].Rectangle.MinXminY,
                            new XYZ(rooms[index].Rectangle.MaxXmaxY.X, contourRectangle.MaxXmaxY.Y, rooms[index].Rectangle.MaxXmaxY.Z));
                }
            }

            private static bool CheckRoomsOnRectangle(List<Room> rooms, Rectangle rectangle)
            {
                return rooms.Any(v => v.Rectangle.IntersectsWith(rectangle));
            }

            private static void ProcessSizesPlacedRooms(List<Room> rooms, int index, Rectangle contourRectangle)
            {
                if (!rooms.Any(r => r == null) && rooms[index].Rectangle.MinXminY.X != contourRectangle.MinXminY.X)
                {
                    Room nearestRoom = GetNearestRoomAxisX(rooms[index], rooms, contourRectangle.MinXminY.X, rooms[index].Rectangle.MinXminY.X);
                    if (nearestRoom != null)
                        if (rooms[index].Rectangle.MinXminY.X - nearestRoom.Rectangle.MaxXmaxY.X >= WidthInnerWall)
                            rooms[index] = ResizeRoom(rooms[index], rooms, contourRectangle,
                                new XYZ(nearestRoom.Rectangle.MaxXmaxY.X + WidthInnerWall, rooms[index].Rectangle.MinXminY.Y, rooms[index].Rectangle.MinXminY.Z),
                                rooms[index].Rectangle.MaxXmaxY);
                        else
                            ReduceRoomHeight(rooms, nearestRoom, rooms[index], contourRectangle);
                }
                if (!rooms.Any(r => r == null) && rooms[index].Rectangle.MaxXmaxY.X != contourRectangle.MaxXmaxY.X)
                {
                    Room nearestRoom = GetNearestRoomAxisX(rooms[index], rooms, rooms[index].Rectangle.MaxXmaxY.X, contourRectangle.MaxXmaxY.X);
                    if (nearestRoom != null)
                        if (nearestRoom.Rectangle.MinXminY.X - rooms[index].Rectangle.MaxXmaxY.X >= WidthInnerWall)
                            rooms[index] = ResizeRoom(rooms[index], rooms, contourRectangle, rooms[index].Rectangle.MinXminY,
                                new XYZ(nearestRoom.Rectangle.MinXminY.X - WidthInnerWall, rooms[index].Rectangle.MaxXmaxY.Y, rooms[index].Rectangle.MaxXmaxY.Z));
                        else
                            ReduceRoomHeight(rooms, rooms[index], nearestRoom, contourRectangle);
                }
                if (!rooms.Any(r => r == null) && rooms[index].Rectangle.MinXminY.Y != contourRectangle.MinXminY.Y)
                {
                    Room nearestRoom = GetNearestRoomAxisY(rooms[index], rooms, contourRectangle.MinXminY.Y, rooms[index].Rectangle.MinXminY.Y);
                    if (nearestRoom != null)
                        if (rooms[index].Rectangle.MinXminY.Y - nearestRoom.Rectangle.MaxXmaxY.Y >= WidthInnerWall)
                            rooms[index] = ResizeRoom(rooms[index], rooms, contourRectangle,
                                new XYZ(rooms[index].Rectangle.MinXminY.X, nearestRoom.Rectangle.MaxXmaxY.Y + WidthInnerWall, rooms[index].Rectangle.MinXminY.Z),
                                rooms[index].Rectangle.MaxXmaxY);
                        else
                            ReduceRoomWidth(rooms, nearestRoom, rooms[index], contourRectangle);
                }
                if (!rooms.Any(r => r == null) && rooms[index].Rectangle.MaxXmaxY.Y != contourRectangle.MaxXmaxY.Y)
                {
                    Room nearestRoom = GetNearestRoomAxisY(rooms[index], rooms, rooms[index].Rectangle.MaxXmaxY.Y, contourRectangle.MaxXmaxY.Y);
                    if (nearestRoom != null)
                        if (nearestRoom.Rectangle.MinXminY.Y - rooms[index].Rectangle.MaxXmaxY.Y >= WidthInnerWall)
                            rooms[index] = ResizeRoom(rooms[index], rooms, contourRectangle, rooms[index].Rectangle.MinXminY,
                                new XYZ(rooms[index].Rectangle.MaxXmaxY.X, nearestRoom.Rectangle.MinXminY.Y - WidthInnerWall, rooms[index].Rectangle.MaxXmaxY.Z));
                        else
                            ReduceRoomWidth(rooms, rooms[index], nearestRoom, contourRectangle);
                }
            }

            private static Room GetNearestRoomAxisX(Room room, List<Room> rooms, double minX, double maxX)
            {
                return rooms.Where(v => minX <= v.Rectangle.MinXminY.X && v.Rectangle.MinXminY.X <= maxX)
                    .Where(v => v.Name != room.Name && v.Rectangle.MinXminY != room.Rectangle.MinXminY)
                    .Where(v => CheckBoundsOnY(room.Rectangle, v.Rectangle))
                    .OrderBy(v =>
                    {
                        if (room.Rectangle.MinXminY.X == maxX)
                            return maxX - v.Rectangle.MaxXmaxY.X;
                        return v.Rectangle.MinXminY.X - minX;
                    })
                    .FirstOrDefault();
            }

            private static Room GetNearestRoomAxisY(Room room, List<Room> rooms, double minY, double maxY)
            {
                return rooms.Where(v => minY <= v.Rectangle.MinXminY.Y && v.Rectangle.MinXminY.Y <= maxY)
                    .Where(v => v.Name != room.Name && v.Rectangle.MinXminY != room.Rectangle.MinXminY)
                    .Where(v => CheckBoundsOnX(room.Rectangle, v.Rectangle))
                    .OrderBy(v =>
                    {
                        if (room.Rectangle.MinXminY.Y == maxY)
                            return maxY - v.Rectangle.MaxXmaxY.Y;
                        return v.Rectangle.MinXminY.Y - minY;
                    })
                    .FirstOrDefault();
            }

            private static bool CheckBoundsOnY(Rectangle rectangle1, Rectangle rectangle2)
            {
                if (rectangle1.MinXminY.Y == rectangle2.MinXminY.Y || rectangle1.MaxXmaxY.Y == rectangle2.MaxXmaxY.Y)
                    return true;
                else if (rectangle1.MinXminY.Y > rectangle2.MinXminY.Y)
                    return rectangle1.MinXminY.Y <= rectangle2.MaxXmaxY.Y;
                else
                    return rectangle1.MaxXmaxY.Y >= rectangle2.MinXminY.Y;
            }

            private static bool CheckBoundsOnX(Rectangle rectangle1, Rectangle rectangle2)
            {
                if (rectangle1.MinXminY.X == rectangle2.MinXminY.X || rectangle1.MaxXmaxY.X == rectangle2.MaxXmaxY.X)
                    return true;
                else if (rectangle1.MinXminY.X > rectangle2.MinXminY.X)
                    return rectangle1.MinXminY.X <= rectangle2.MaxXmaxY.X;
                else
                    return rectangle1.MaxXmaxY.X >= rectangle2.MinXminY.X;
            }

            private static Room ResizeRoom(Room room, List<Room> rooms, Rectangle contourRectangle, XYZ pointMin, XYZ pointMax)
            {
                Room newVisiting = room.CreateNew(new Rectangle(pointMin, pointMax));
                newVisiting = ProcessingIntersections(newVisiting, rooms);
                if (newVisiting != null)
                    room = newVisiting;

                return room;
            }

            private static void ReduceRoomHeight(List<Room> rooms, Room roomLeft, Room roomRight, Rectangle contourRectangle)
            {
                double sizeReduction = WidthInnerWall - (roomRight.Rectangle.MinXminY.X - roomLeft.Rectangle.MaxXmaxY.X);
                int indexLeft = rooms.IndexOf(roomLeft);
                int indexRight = rooms.IndexOf(roomRight);

                if (roomLeft.CanReduceHeightBy(sizeReduction))
                    rooms[indexLeft] = ResizeRoom(roomLeft, rooms, contourRectangle, roomLeft.Rectangle.MinXminY,
                        roomLeft.Rectangle.MaxXmaxY - new XYZ(sizeReduction, 0, 0));
                else if (roomRight.CanReduceHeightBy(sizeReduction))
                    rooms[indexRight] = ResizeRoom(roomRight, rooms, contourRectangle, roomRight.Rectangle.MinXminY + new XYZ(sizeReduction, 0, 0),
                        roomRight.Rectangle.MaxXmaxY);
                else
                {
                    rooms[indexLeft] = null;
                    rooms[indexRight] = null;
                }
            }

            private static void ReduceRoomWidth(List<Room> rooms, Room roomBottom, Room roomTop, Rectangle contourRectangle)
            {
                double sizeReduction = WidthInnerWall - (roomTop.Rectangle.MinXminY.Y - roomBottom.Rectangle.MaxXmaxY.Y);
                int indexBottom = rooms.IndexOf(roomBottom);
                int indexTop = rooms.IndexOf(roomTop);

                if (roomBottom.CanReduceWidthBy(sizeReduction))
                    rooms[indexBottom] = ResizeRoom(roomBottom, rooms, contourRectangle, roomBottom.Rectangle.MinXminY,
                        roomBottom.Rectangle.MaxXmaxY - new XYZ(0, sizeReduction, 0));
                else if (roomTop.CanReduceWidthBy(sizeReduction))
                    rooms[indexTop] = ResizeRoom(roomTop, rooms, contourRectangle, roomTop.Rectangle.MinXminY + new XYZ(0, sizeReduction, 0),
                        roomTop.Rectangle.MaxXmaxY);
                else
                {
                    rooms[indexBottom] = null;
                    rooms[indexTop] = null;
                }
            }

            public static void CreateNewDirectShape(Document doc, XYZ pointMin, XYZ pointMax, List<List<Room>> variants)
            {
                    var minPosition = new XYZ(0, 0, 0);
                    foreach (var variant in variants)
                    {
                        foreach (var room in variant)
                        {
                            var points = room.Rectangle.ExtremePoints.Select(p => p + minPosition).ToArray();
                            var cL = new CurveLoop();

                            cL.Append(Line.CreateBound(points[0], points[1]));
                            cL.Append(Line.CreateBound(points[1], points[2]));
                            cL.Append(Line.CreateBound(points[2], points[3]));
                            cL.Append(Line.CreateBound(points[3], points[0]));

                            var curveLoops = new List<CurveLoop>() { cL };
                            var solidOptions = new SolidOptions(ElementId.InvalidElementId, ElementId.InvalidElementId);
                            var solid = GeometryCreationUtilities.CreateExtrusionGeometry(curveLoops, XYZ.BasisZ, 13,
                                solidOptions);

                            var directShape = DirectShape.CreateElement(doc, new ElementId(BuiltInCategory.OST_GenericModel));
                            directShape.Name = room.Name;
                            directShape.SetShape(new List<GeometryObject>() { solid });
                        }
                        minPosition += new XYZ(30, 0, 0);
                    }
                    TaskDialog.Show("DirectShape", $"Completed");
            }

            public void Run()
            {
                ContourFlat contour = GetContourFlat(CommandData.Application.ActiveUIDocument.Document);
                List<List<Room>> variants = Generate(CreateRooms(), contour);
                Rectangle rect = contour.GeometricShape as Rectangle;
                CreateNewDirectShape(CommandData.Application.ActiveUIDocument.Document, rect.MinXminY, rect.MaxXmaxY, variants);
            }
        }
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            CommandData = commandData;
            UserWindowControl userWindowControl = new UserWindowControl();
            userWindowControl.Show();
            return Result.Succeeded;
        }
    }
}