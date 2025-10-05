using System;

namespace UnityEngine
{
    public class MonoBehaviour
    {
    }

    public struct Color
    {
        public float r;
        public float g;
        public float b;
        public float a;

        public Color(float red, float green, float blue, float alpha = 1f)
        {
            r = red;
            g = green;
            b = blue;
            a = alpha;
        }

        public static Color cyan => new Color(0f, 1f, 1f);
        public static Color red => new Color(1f, 0f, 0f);
    }

    public struct Vector3
    {
        public float x;
        public float y;
        public float z;

        public Vector3(float x, float y, float z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public static float Distance(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dy = a.y - b.y;
            float dz = a.z - b.z;
            return (float)Math.Sqrt((dx * dx) + (dy * dy) + (dz * dz));
        }
    }

    public struct Quaternion
    {
        public Vector3 eulerAngles { get; set; }

        public Quaternion(Vector3 eulerAngles)
        {
            this.eulerAngles = eulerAngles;
        }
    }

    public class Transform
    {
        public Vector3 position { get; set; }
        public Quaternion rotation { get; set; }

        public Transform()
        {
            position = new Vector3();
            rotation = new Quaternion(new Vector3());
        }
    }
}
