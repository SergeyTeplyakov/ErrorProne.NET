using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ErrorProne.Samples
{
    enum ShapeType
    {
        Circle,
        Rectangle,
        Square
    }
    abstract class Shape
    {
        public static Shape CreateShape(ShapeType shapeType)
        {
            // Warning: Possible missed enum case(s) 'Square' int the switch statemet
            switch(shapeType)
            {
                case ShapeType.Circle: return new Circle();
                case ShapeType.Rectangle:return new Rectangle();
                default: throw new InvalidOperationException($"Unknown shape type '{shapeType}'");
            }
        }
    }
    class Circle : Shape { }
    class Rectangle : Shape { }
}
