using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace _2D_Fluid_Sim_GPU
{
    internal class Sim
    {
        static Vector2[,] velocities = new Vector2[400, 400];
        public static Vector2[,] GetVelocities()
        {
            return velocities;
        }
        public static void UpdateSimulation(float t)
        {
            //ApplyGravity(t);
            Advection(t);
        }
        public static void InitialiseVelocities()
        {
            for (int i = 0; i < 400; i++)
            {
                for (int j = 0; j < 400; j++)
                {
                    velocities[i, j] = new Vector2(0, 0);
                }
            }
        }
        public static void ApplyMouseAcceleration(int x, int y, Vector2 delta, float deltaTime)
        {
            Vector2 currentPos = new Vector2(x, y);
            Vector2 velocity = new Vector2(delta.X / deltaTime, delta.Y / deltaTime);

            int radius = 1;
            float strength = 2f;
            for (int i = x - radius; i <= x + radius; i++)
            {
                for (int j = y - radius; j <= y + radius; j++)
                {
                    if (i >= 0 && j >= 0 && i < velocities.GetLength(0) && j < velocities.GetLength(1))
                    {
                        float dx = i - x;
                        float dy = j - y;
                        float dist = MathF.Sqrt(dx * dx + dy * dy);

                        if (dist <= radius)
                        {
                            float weight = 1.0f - (dist / radius);
                            velocities[i, j] = new Vector2(velocities[i, j].X + velocity.X * weight * strength, velocities[i, j].Y + velocity.Y * weight * strength);
                        }
                    }
                }
            }
        }
        static void Advection(float t)
        {
            Vector2[,] newVelocities = new Vector2[400, 400];

            Parallel.For(0, 400, y =>
            {
                for (int x = 0; x < 400; x++)
                {
                    Vector2 v = velocities[x, y];
                    Vector2 pos = new Vector2(x, y) - new Vector2(v.X * t, v.Y * t);
                    newVelocities[x, y] = SamplePropertiesAtPos(pos);
                    if (float.IsNaN(newVelocities[x, y].X))
                    {
                        newVelocities[x, y] = new Vector2(-velocities[x, y].X, -velocities[x, y].Y);
                    }
                }
            });

            // After parallel processing, copy results back to main arrays
            Parallel.For(0, 400, y =>
            {
                for (int x = 0; x < 400; x++)
                {
                    velocities[x, y] = newVelocities[x, y];
                }
            });
        }
        static Vector2 SamplePropertiesAtPos(Vector2 pos)
        {
            float damping = 0.98f;
            int x = Convert.ToInt32(Math.Floor(pos.X));
            int y = Convert.ToInt32(Math.Floor(pos.Y));
            Vector2 vResult = new Vector2(0, 0);
            int samples = 0;
            for (int i = x - 5; i < x + 5; i++)
            {
                for (int j = y - 5; j < y + 5; j++)
                {
                    if (i > 0 && j > 0 && i < 400 && j < 400)
                    {
                        vResult += velocities[i, j];
                        samples++;
                    }
                }
            }
            if (samples != 0)
            {
                vResult = new Vector2(vResult.X / samples, vResult.Y / samples);
                return vResult * damping;
            }
            else
            {
                return new Vector2(float.NaN, float.NaN);
            }
        }
    }
}
