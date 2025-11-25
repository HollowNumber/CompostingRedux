using Vintagestory.API.Client;
using Vintagestory.API.MathTools;

namespace CompostingRedux.Utils
{
    public static class MeshUtil
    {
        public static MeshData GetCubeMesh(float xMin, float yMin, float zMin, float xMax, float yMax, float zMax,
            TextureAtlasPosition tex)
        {
            if (tex == null) return new MeshData(4, 6);

            MeshData mesh = new MeshData(24, 36, true, true, true, true);
            mesh.VerticesCount = 24;
            mesh.IndicesCount = 36;

            // --- Vertices ---
            // Top Face (yMax)
            SetVert(mesh, 0, xMin, yMax, zMax);
            SetVert(mesh, 1, xMax, yMax, zMax);
            SetVert(mesh, 2, xMax, yMax, zMin);
            SetVert(mesh, 3, xMin, yMax, zMin);

            // Bottom Face (yMin)
            SetVert(mesh, 4, xMin, yMin, zMax);
            SetVert(mesh, 5, xMin, yMin, zMin);
            SetVert(mesh, 6, xMax, yMin, zMin);
            SetVert(mesh, 7, xMax, yMin, zMax);

            // Front Face (South, zMax)
            SetVert(mesh, 8, xMin, yMin, zMax);
            SetVert(mesh, 9, xMax, yMin, zMax);
            SetVert(mesh, 10, xMax, yMax, zMax);
            SetVert(mesh, 11, xMin, yMax, zMax);

            // Back Face (North, zMin)
            SetVert(mesh, 12, xMax, yMin, zMin);
            SetVert(mesh, 13, xMin, yMin, zMin);
            SetVert(mesh, 14, xMin, yMax, zMin);
            SetVert(mesh, 15, xMax, yMax, zMin);

            // Left Face (West, xMin)
            SetVert(mesh, 16, xMin, yMin, zMin);
            SetVert(mesh, 17, xMin, yMin, zMax);
            SetVert(mesh, 18, xMin, yMax, zMax);
            SetVert(mesh, 19, xMin, yMax, zMin);

            // Right Face (East, xMax)
            SetVert(mesh, 20, xMax, yMin, zMax);
            SetVert(mesh, 21, xMax, yMin, zMin);
            SetVert(mesh, 22, xMax, yMax, zMin);
            SetVert(mesh, 23, xMax, yMax, zMax);

            // --- UVs ---
            // We map the entire texture to every face to ensure visibility.
            // No cropping, no fancy math. Just show the texture.

            float u1 = tex.x1; float u2 = tex.x2;
            float v1 = tex.y1; float v2 = tex.y2;
            
            // Top
            SetUV(mesh, 0, u1, v2); // xMin, zMax
            SetUV(mesh, 1, u2, v2); // xMax, zMax
            SetUV(mesh, 2, u2, v1); // xMax, zMin
            SetUV(mesh, 3, u1, v1); // xMin, zMin
            // Bottom
            SetUV(mesh, 4, u1, v2);
            SetUV(mesh, 5, u1, v1);
            SetUV(mesh, 6, u2, v1);
            SetUV(mesh, 7, u2, v2);
            // Front
            SetUV(mesh, 8, u1, v2);
            SetUV(mesh, 9, u2, v2);
            SetUV(mesh, 10, u2, v1);
            SetUV(mesh, 11, u1, v1);
            // Back
            SetUV(mesh, 12, u1, v2);
            SetUV(mesh, 13, u2, v2);
            SetUV(mesh, 14, u2, v1);
            SetUV(mesh, 15, u1, v1);
            // Left
            SetUV(mesh, 16, u1, v2);
            SetUV(mesh, 17, u2, v2);
            SetUV(mesh, 18, u2, v1);
            SetUV(mesh, 19, u1, v1);
            // Right
            SetUV(mesh, 20, u1, v2);
            SetUV(mesh, 21, u2, v2);
            SetUV(mesh, 22, u2, v1);
            SetUV(mesh, 23, u1, v1);

            // --- Colors & Normals ---
            for (int i = 0; i < 24; i++)
            {
                // White
                mesh.Rgba[i * 4] = 255;
                mesh.Rgba[i * 4 + 1] = 255;
                mesh.Rgba[i * 4 + 2] = 255;
                mesh.Rgba[i * 4 + 3] = 255;
                // Up normal for simplicity (lit from top)
                // Usually you vary this per face, but 0,1,0 is safe for compost piles
                mesh.Flags[i] = 0; // Reset flags
            }

            // Manual Normals per face (Better lighting)
            // Top
            for (int i = 0; i < 4; i++)
            {
                mesh.AddNormal(0, 1, 0);
            }

            // Bottom
            for (int i = 0; i < 4; i++)
            {
                mesh.AddNormal(0, -1, 0);
            }

            // Front
            for (int i = 0; i < 4; i++)
            {
                mesh.AddNormal(0, 0, 1);
            }

            // Back
            for (int i = 0; i < 4; i++)
            {
                mesh.AddNormal(0, 0, -1);
            }

            // Left
            for (int i = 0; i < 4; i++)
            {
                mesh.AddNormal(-1, 0, 0);
            }

            // Right
            for (int i = 0; i < 4; i++)
            {
                mesh.AddNormal(1, 0, 0);
            }

            // --- Indices ---
            int[] pattern = new int[] { 0,1,2, 0,2,3 };
            for (int face = 0; face < 6; face++)
            {
                // Invert Top Face (Face 0) winding if it was invisible
                if (face == 0) 
                {
                    // Try 0,2,1 and 0,3,2 to flip normal
                    int baseI = 0;
                    mesh.Indices[0] = baseI + 0; mesh.Indices[1] = baseI + 2; mesh.Indices[2] = baseI + 1;
                    mesh.Indices[3] = baseI + 0; mesh.Indices[4] = baseI + 3; mesh.Indices[5] = baseI + 2;
                    continue;
                }
                
                int baseI2 = face * 4;
                for (int k = 0; k < 6; k++) mesh.Indices[face * 6 + k] = baseI2 + pattern[k];
            }
            return mesh;
        }

        private static void SetVert(MeshData m, int i, float x, float y, float z)
        {
            m.xyz[i * 3] = x;
            m.xyz[i * 3 + 1] = y;
            m.xyz[i * 3 + 2] = z;
        }

        private static void SetUV(MeshData m, int i, float u, float v)
        {
            m.Uv[i * 2] = u;
            m.Uv[i * 2 + 1] = v;
        }
    }
}