using Autodesk.Revit.DB;

namespace purrpurrPlugin
{
    public abstract class Room
    {
        public abstract string Name { get; }
        protected abstract double MinWidthMeter { get; }
        protected abstract double MinHeightMeter { get; }
        protected abstract double MinSquareMeter { get; }
        public abstract bool canNearWindow { get; }

        protected XYZ minPoint;

        protected double widthMeter;
        public double WidthMeter => widthMeter;
        public double WidthFeet => widthMeter / 0.3048;

        protected double heightMeter;
        public double HeightMeter => heightMeter;
        public double HeightFeet => heightMeter / 0.3048;

        protected double squareMeter;
        public double SquareMeter => squareMeter;

        protected Rectangle rectangle;
        public Rectangle Rectangle => rectangle;

        public Room()
        {
            widthMeter = MinWidthMeter;
            heightMeter = MinHeightMeter;
            squareMeter = widthMeter * heightMeter;
        }

        public Room(XYZ minPoint)
        {
            this.minPoint = minPoint;
            widthMeter = MinWidthMeter;
            squareMeter = MinSquareMeter;
            heightMeter = squareMeter / widthMeter;
            rectangle = new Rectangle(minPoint, WidthFeet, HeightFeet);
        }

        public Room(XYZ minPoint, double widthMeter = 0.0, double heightMeter = 0.0, double squareMeter = 0.0)
        {
            this.minPoint = minPoint;

            if (widthMeter == 0)
                widthMeter = squareMeter / heightMeter;
            if (heightMeter == 0)
                heightMeter = squareMeter / widthMeter;
            if (squareMeter == 0)
                squareMeter = widthMeter * heightMeter;

            this.widthMeter = widthMeter;
            this.heightMeter = heightMeter;
            this.squareMeter = squareMeter;
            rectangle = new Rectangle(minPoint, WidthFeet, HeightFeet);
        }

        public Room(Rectangle rectangle)
        {
            this.rectangle = rectangle;

            minPoint = rectangle.MinXminY;
            widthMeter = rectangle.Width.LengthOnMeter;
            heightMeter = rectangle.Height.LengthOnMeter;
            squareMeter = rectangle.SquareMeter;
        }

        public void RotatePerpendicular()
        {
            (widthMeter, heightMeter) = (heightMeter, widthMeter);
        }

        public bool CanReduceWidthBy(double value) => WidthMeter - value * 0.3048 >= MinWidthMeter;

        public bool CanReduceHeightBy(double value) => HeightMeter - value * 0.3048 >= MinHeightMeter;

        public abstract Room CreateNew(Rectangle newRectangle);
        public abstract Room CreateNew(XYZ pointMin, XYZ pointMax);

        public abstract bool IsCorectPositionRelativeWalls(Side wallWithDoor, Side wallWithWindow);
    }
    internal class Bathroom : Room
    {
        public override string Name => "Bathroom";

        protected override double MinWidthMeter => 1.65;

        protected override double MinHeightMeter => 1.65;

        protected override double MinSquareMeter => MinWidthMeter * MinHeightMeter;

        public override bool canNearWindow => false;

        public Bathroom() : base() { }

        public Bathroom(XYZ minPoint) : base(minPoint) { }

        public Bathroom(XYZ minPoint, double widthMeter = 0, double heightMeter = 0, double squareMeter = 0) :
            base(minPoint, widthMeter, heightMeter, squareMeter)
        { }

        public Bathroom(Rectangle rectangle) : base(rectangle) { }

        public override Room CreateNew(Rectangle newRectangle)
        {
            return new Bathroom(newRectangle);
        }

        public override Room CreateNew(XYZ pointMin, XYZ pointMax)
        {
            return new Bathroom(new Rectangle(pointMin, pointMax));
        }

        public override bool IsCorectPositionRelativeWalls(Side wallWithDoor, Side wallWithWindow)
        {
            foreach (XYZ point in rectangle.ExtremePoints)
                if (wallWithWindow.Contains(point))
                    return false;

            return true;
        }
    }
    public class Hallway : Room
    {
        public override string Name => "Hallway";

        protected override double MinWidthMeter => 1.1;

        protected override double MinHeightMeter => 1.1;

        protected override double MinSquareMeter => MinWidthMeter * MinHeightMeter;

        public override bool canNearWindow => false;

        public Hallway() : base() { }

        public Hallway(XYZ minPoint) : base(minPoint) { }

        public Hallway(XYZ minPoint, double widthMeter = 0, double heightMeter = 0, double squareMeter = 0) :
            base(minPoint, widthMeter, heightMeter, squareMeter)
        { }

        public Hallway(Rectangle rectangle) : base(rectangle) { }

        public override Room CreateNew(Rectangle newRectangle)
        {
            return new Hallway(newRectangle);
        }

        public override Room CreateNew(XYZ pointMin, XYZ pointMax)
        {
            return new Hallway(new Rectangle(pointMin, pointMax));
        }

        public override bool IsCorectPositionRelativeWalls(Side wallWithDoor, Side wallWithWindow)
        {
            for (int i = 0; i < rectangle.ExtremePoints.Length; i++)
            {
                if (wallWithDoor.Contains(rectangle.ExtremePoints[i]))
                    return true;
            }

            return false;
        }
    }
    public class Kitchen : Room
    {
        public override string Name => "Kitchen";

        protected override double MinWidthMeter => 2.8;

        protected override double MinHeightMeter => 2.8;

        protected override double MinSquareMeter => MinWidthMeter * MinHeightMeter;

        public override bool canNearWindow => true;

        public Kitchen() : base() { }

        public Kitchen(XYZ minPoint) : base(minPoint) { }

        public Kitchen(XYZ minPoint, double widthMeter = 0, double heightMeter = 0, double squareMeter = 0) :
            base(minPoint, widthMeter, heightMeter, squareMeter)
        { }

        public Kitchen(Rectangle rectangle) : base(rectangle) { }

        public override Room CreateNew(Rectangle newRectangle)
        {
            return new Kitchen(newRectangle);
        }

        public override Room CreateNew(XYZ pointMin, XYZ pointMax)
        {
            return new Kitchen(new Rectangle(pointMin, pointMax));
        }

        public override bool IsCorectPositionRelativeWalls(Side wallWithDoor, Side wallWithWindow)
        {
            return true;
        }
    }
    internal class LivingRoom : Room
    {
        public override string Name => "Living Room";

        protected override double MinWidthMeter => 3;

        protected override double MinHeightMeter => 3;

        protected override double MinSquareMeter => MinWidthMeter * MinHeightMeter;

        public override bool canNearWindow => true;

        public LivingRoom() : base() { }

        public LivingRoom(XYZ minPoint) : base(minPoint) { }

        public LivingRoom(XYZ minPoint, double widthMeter = 0, double heightMeter = 0, double squareMeter = 0) :
            base(minPoint, widthMeter, heightMeter, squareMeter)
        { }

        public LivingRoom(Rectangle rectangle) : base(rectangle) { }

        public override Room CreateNew(Rectangle newRectangle)
        {
            return new LivingRoom(newRectangle);
        }

        public override Room CreateNew(XYZ pointMin, XYZ pointMax)
        {
            return new LivingRoom(new Rectangle(pointMin, pointMax));
        }

        public override bool IsCorectPositionRelativeWalls(Side wallWithDoor, Side wallWithWindow)
        {
            return true;
        }
    }
}