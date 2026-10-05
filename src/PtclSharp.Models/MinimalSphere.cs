using System.Numerics;

namespace PtclSharp.Models;

/// <summary>The smallest sphere containing a set of points (Welzl's algorithm), in double precision.</summary>
internal static class MinimalSphere
{
    internal static (Vector3 Center, float Radius) Of(IReadOnlyList<Vector3> points)
    {
        // A fixed shuffle keeps the expected running time linear without making the result depend on chance.
        var shuffled = points.Select(p => new double[] { p.X, p.Y, p.Z }).ToArray();
        var random = new Random(12345);
        for (int i = shuffled.Length - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
        }

        (double[] center, double radius2) = Welzl(shuffled, shuffled.Length, []);
        double slack = 1.0 + 1e-9;
        return (new Vector3((float)center[0], (float)center[1], (float)center[2]), (float)Math.Sqrt(radius2 * slack));
    }

    private static (double[] Center, double Radius2) Welzl(double[][] p, int n, List<double[]> support)
    {
        (double[] center, double radius2) = Trivial(support);
        if (support.Count == 4) return (center, radius2);
        for (int i = 0; i < n; i++)
        {
            if (Distance2(p[i], center) <= radius2 * (1 + 1e-12) + 1e-18) continue;
            var next = new List<double[]>(support) { p[i] };
            (center, radius2) = Welzl(p, i, next);
        }
        return (center, radius2);
    }

    private static (double[] Center, double Radius2) Trivial(List<double[]> s)
    {
        switch (s.Count)
        {
            case 0: return ([0, 0, 0], -1);
            case 1: return (s[0], 0);
            case 2:
            {
                double[] c = [(s[0][0] + s[1][0]) / 2, (s[0][1] + s[1][1]) / 2, (s[0][2] + s[1][2]) / 2];
                return (c, Distance2(c, s[0]));
            }
            case 3: return Circumcircle(s[0], s[1], s[2]);
            default: return Circumsphere(s[0], s[1], s[2], s[3]);
        }
    }

    private static (double[] Center, double Radius2) Circumcircle(double[] a, double[] b, double[] c)
    {
        double[] ab = Sub(b, a), ac = Sub(c, a);
        double[] cross = Cross(ab, ac);
        double denominator = 2 * Dot(cross, cross);
        if (denominator < 1e-30) // collinear: the sphere on the two farthest points
        {
            var pairs = new[] { (a, b), (a, c), (b, c) };
            (double[] u, double[] v) = pairs.OrderByDescending(t => Distance2(t.Item1, t.Item2)).First();
            double[] mid = [(u[0] + v[0]) / 2, (u[1] + v[1]) / 2, (u[2] + v[2]) / 2];
            return (mid, Distance2(mid, u));
        }
        double[] t1 = Scale(Cross(cross, ab), Dot(ac, ac));
        double[] t2 = Scale(Cross(ac, cross), Dot(ab, ab));
        double[] offset = Scale(Add(t1, t2), 1 / denominator);
        double[] center = Add(a, offset);
        return (center, Dot(offset, offset));
    }

    private static (double[] Center, double Radius2) Circumsphere(double[] a, double[] b, double[] c, double[] d)
    {
        double[] ba = Sub(b, a), ca = Sub(c, a), da = Sub(d, a);
        double det = 2 * Dot(ba, Cross(ca, da));
        if (Math.Abs(det) < 1e-30) // coplanar: the smallest circle through three of the four
        {
            var candidates = new[] { Circumcircle(a, b, c), Circumcircle(a, b, d), Circumcircle(a, c, d), Circumcircle(b, c, d) };
            return candidates.Where(s => new[] { a, b, c, d }.All(q => Distance2(q, s.Center) <= s.Radius2 * (1 + 1e-9) + 1e-18)).OrderBy(s => s.Radius2).First();
        }
        double[] offset = Scale(Add(Add(Scale(Cross(ca, da), Dot(ba, ba)), Scale(Cross(da, ba), Dot(ca, ca))), Scale(Cross(ba, ca), Dot(da, da))), 1 / det);
        return (Add(a, offset), Dot(offset, offset));
    }

    private static double Distance2(double[] a, double[] b) => ((a[0] - b[0]) * (a[0] - b[0])) + ((a[1] - b[1]) * (a[1] - b[1])) + ((a[2] - b[2]) * (a[2] - b[2]));
    private static double[] Sub(double[] a, double[] b) => [a[0] - b[0], a[1] - b[1], a[2] - b[2]];
    private static double[] Add(double[] a, double[] b) => [a[0] + b[0], a[1] + b[1], a[2] + b[2]];
    private static double[] Scale(double[] a, double s) => [a[0] * s, a[1] * s, a[2] * s];
    private static double Dot(double[] a, double[] b) => (a[0] * b[0]) + (a[1] * b[1]) + (a[2] * b[2]);
    private static double[] Cross(double[] a, double[] b) => [(a[1] * b[2]) - (a[2] * b[1]), (a[2] * b[0]) - (a[0] * b[2]), (a[0] * b[1]) - (a[1] * b[0])];
}
