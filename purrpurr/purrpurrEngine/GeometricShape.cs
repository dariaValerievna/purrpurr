using Autodesk.Revit.DB;
using System;

namespace purrpurrPlugin
{
    public class Side
    {
        public XYZ Start { get; private set; }
        public XYZ End { get; private set; }
        public double LengthOnFeet { get => GetLengthOnFeet(); }
        public double LengthOnMeter { get => Math.Round(LengthOnFeet * 0.3048, 3); }

        public Side(XYZ start, XYZ end)
        {
            Start = start;
            End = end;
        }

        private double GetLengthOnFeet()
        {
            return Math.Sqrt(Math.Pow(End.X - Start.X, 2) + Math.Pow(End.Y - Start.Y, 2));
        }

        public bool Contains(XYZ point)
        {
            return Math.Min(Start.X, End.X) <= point.X && point.X <= Math.Max(Start.X, End.X) &&
                Math.Min(Start.Y, End.Y) <= point.Y && point.Y <= Math.Max(Start.Y, End.Y);
        }

        public double DistanceToPoint(XYZ point)
        {
            double lenToPointMin = Math.Sqrt(Math.Pow(point.X - Start.X, 2) + Math.Pow(point.Y - Start.Y, 2));
            double lenToPointMax = Math.Sqrt(Math.Pow(point.X - End.X, 2) + Math.Pow(point.Y - End.Y, 2));
            double halfMeter = (LengthOnFeet + lenToPointMax + lenToPointMin) / 2;
            double square = Math.Sqrt(halfMeter * (halfMeter - LengthOnFeet) * (halfMeter - lenToPointMin) * (halfMeter - lenToPointMax));
            double scalarToPointMin = (point.X - Start.X) * (End.X - Start.X) + (point.Y - Start.Y) * (End.Y - Start.Y);
            double scalarToPointMax = (point.X - End.X) * (Start.X - End.X) + (point.Y - End.Y) * (Start.Y - End.Y);

            if (scalarToPointMax > 0 && scalarToPointMin > 0)
                return square * 2 / LengthOnFeet;
            else if (scalarToPointMin <= 0 || scalarToPointMax <= 0)
                return Math.Min(lenToPointMin, lenToPointMax);
            else
                return 0;
        }

        public XYZ GetIntersection(Side line)
        {
            double x1 = Math.Round(Start.X, 4);
            double y1 = Math.Round(Start.Y, 4);
            double x2 = Math.Round(End.X, 4);
            double y2 = Math.Round(End.Y, 4);
            double x3 = Math.Round(line.Start.X, 4);
            double y3 = Math.Round(line.Start.Y, 4);
            double x4 = Math.Round(line.End.X, 4);
            double y4 = Math.Round(line.End.Y, 4);
            double k1 = (x2 - x1) / (y2 - y1);
            double k2 = (x4 - x3) / (y4 - y3);
            if (k1 == k2)
                return null;
            double x = Math.Round(((x1 * y2 - x2 * y1) * (x4 - x3) - (x3 * y4 - x4 * y3) * (x2 - x1)) / ((y1 - y2) * (x4 - x3) - (y3 - y4) * (x2 - x1)), 4);
            double y = Math.Round(((y3 - y4) * x - (x3 * y4 - x4 * y3)) / (x4 - x3), 4);
            if (((x1 <= x) && (x2 >= x) && (x3 <= x) && (x4 >= x)) || ((y1 <= y) && (y2 >= y) && (y3 <= y) && (y4 >= y)))
                return new XYZ(x, y, 0);
            return null;
        }

        public override string ToString()
        {
            return $"PointMin - {Start}\nPointMax - {End}";
        }
    }
    public interface IGeometricShape
    {
        XYZ[] ExtremePoints { get; }
        Side[] Sides { get; }
        double SquareMeter { get; }
        double SquareFeet { get; }

        bool Contains(XYZ point);
        bool Contains(Rectangle rectangle);
    }
    public class Rectangle : IGeometricShape
    {
        private readonly XYZ minXminY;
        private readonly XYZ maxXminY;
        private readonly XYZ maxXmaxY;
        private readonly XYZ minXmaxY;

        public XYZ[] ExtremePoints
        {
            get
            {
                return new XYZ[]
                {
                    minXminY,
                    maxXminY,
                    maxXmaxY,
                    minXmaxY
                };
            }
        }
        public Side[] Sides
        {
            get
            {
                return new Side[4]
                {
                    new Side(minXminY, minXmaxY),
                    new Side(minXmaxY, maxXmaxY),
                    new Side(maxXminY, maxXmaxY),
                    new Side(minXminY, maxXminY),
                };
            }
        }

        public XYZ MinXminY { get => minXminY; }
        public XYZ MaxXminY { get => maxXminY; }
        public XYZ MaxXmaxY { get => maxXmaxY; }
        public XYZ MinXmaxY { get => minXmaxY; }

        public Side Width { get => Sides[1]; }
        public Side Height { get => Sides[0]; }

        public double SquareMeter { get => Width.LengthOnMeter * Height.LengthOnMeter; }
        public double SquareFeet { get => Width.LengthOnFeet * Height.LengthOnFeet; }

        public Rectangle(XYZ minXminY, XYZ maxXmaxY)
        {
            this.minXminY = minXminY;
            this.maxXmaxY = maxXmaxY;

            maxXminY = new XYZ(maxXmaxY.X, minXminY.Y, minXminY.Z);
            minXmaxY = new XYZ(minXminY.X, maxXmaxY.Y, minXminY.Z);
        }

        public Rectangle(XYZ minXminY, XYZ maxXminY, XYZ maxXmaxY, XYZ minXmaxY)
        {
            this.minXminY = minXminY;
            this.maxXminY = maxXminY;
            this.maxXmaxY = maxXmaxY;
            this.minXmaxY = minXmaxY;
        }

        public Rectangle(XYZ[] points)
        {
            minXminY = points[0];
            maxXminY = points[1];
            maxXmaxY = points[2];
            minXmaxY = points[3];
        }

        public Rectangle(XYZ minXminY, double widthFeet, double heightFeet)
        {
            this.minXminY = minXminY;
            maxXminY = new XYZ(minXminY.X + widthFeet, minXminY.Y, minXminY.Z);
            maxXmaxY = new XYZ(minXminY.X + widthFeet, minXminY.Y + heightFeet, minXminY.Z);
            minXmaxY = new XYZ(minXminY.X, minXminY.Y + heightFeet, minXminY.Z);
        }

        public bool IntersectsWith(Rectangle other)
        {
            return !(MinXminY.X > other.MaxXmaxY.X || other.MinXminY.X > MaxXmaxY.X ||
                MinXminY.Y > other.MaxXmaxY.Y || other.MinXminY.Y > MaxXmaxY.Y);
        }

        public bool Contains(XYZ point)
        {
            return (MinXminY.X <= point.X && point.X <= MaxXminY.X) && (MinXminY.Y <= point.Y && point.Y <= MaxXmaxY.Y);
        }

        public bool Contains(Rectangle other)
        {
            return (MinXminY.X <= other.MinXminY.X && MinXminY.Y <= other.MinXminY.Y &&
                other.MaxXmaxY.X <= MaxXmaxY.X && other.MaxXmaxY.Y <= MaxXmaxY.Y);
        }

        public Rectangle GetIntersectionRectangle(Rectangle other)
        {
            if (IntersectsWith(other))
            {
                double minX = Math.Max(MinXminY.X, other.MinXminY.X);
                double minY = Math.Max(MinXminY.Y, other.MinXminY.Y);
                double maxX = Math.Min(MaxXmaxY.X, other.MaxXmaxY.X);
                double maxY = Math.Min(MaxXmaxY.Y, other.MaxXmaxY.Y);
                double pointZ = MinXminY.Z;

                return new Rectangle(new XYZ(minX, minY, pointZ), new XYZ(maxX, minY, pointZ),
                    new XYZ(maxX, maxY, pointZ), new XYZ(minX, maxY, pointZ));
            }

            return null;
        }

        public override string ToString()
        {
            return $"MinX_MinY - {MinXminY}\n" +
                $"MaxX_MinY{MaxXminY}\n" +
                $"MaxX_MaxY - {MaxXmaxY}\n" +
                $"MinX_MaxY{MinXmaxY}";
        }
    }
}
