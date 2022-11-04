using UnityEditor;
using UnityEngine;

internal static class GeometryPhysics
{
    public static bool IntersectRayTriangle(Ray ray, Vector3 a, Vector3 b, Vector3 c, out RaycastHit hit, bool isBidirectional)
    {
        hit = new RaycastHit();

        Vector3 BA = b - a;
        Vector3 CA = c - a;

        // Get triangle normal 

        Vector3 triangleNormal = Vector3.Cross(BA, CA);

        // Check if the ray and normal are parallel

        float num = Vector3.Dot(-ray.direction, triangleNormal);
        if (num <= 0f)
        {
            return false;
        }
        
        // 
        Vector3 rayVector = ray.origin - a;
        float num2 = Vector3.Dot(rayVector, triangleNormal);

        if (!isBidirectional && num2 < 0f)
        {
            return false;
        }

        // Final checks to make sure the ray is contained by CA and BA

        Vector3 rayCross = Vector3.Cross(-ray.direction, rayVector);    
        float num3 = Vector3.Dot(CA, rayCross);
        if (num3 < 0f || num3 > num)
        {
            return false;
        }

        float num4 = 0f - Vector3.Dot(BA, rayCross);
        if (num4 < 0f || num3 + num4 > num)
        {
            return false;
        }

        // Normalize normal
        float num5 = 1f / num;
        num2 *= num5;
        num3 *= num5;
        num4 *= num5;
        float x = 1f - num3 - num4;

        hit.point = ray.origin + num2 * ray.direction;
        hit.distance = num2;
        hit.normal = Vector3.Normalize(triangleNormal);
        hit.barycentricCoordinate = new Vector3(x, num3, num4);

        return true;
    }
}
