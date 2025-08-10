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
        static float[,] pressure = new float[400,400];
        public static Vector2[,] GetVelocities()
        {
            return velocities;
        }
        public static void SetVelocities(Vector2[,] ivelocities)
        {
            velocities = ivelocities;
        }
        public static void SetPressure(float[] pressureArray, int width, int height)
        {
            pressure = new float[width, height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    pressure[x, y] = pressureArray[y * width + x];
                }
            }
        }
        public static void UpdateSimulation(float t)
        {
            //ApplyGravity(t);
            //Diffusion(t);
            EnforceVelocityBoundary();
            Pressure();
            EnforcePressureBoundary();
            Advection(t);
            EnforceVelocityBoundary();
        }
        public static void Pressure()
        {
            for (int x = 1; x < 399; x++)
            {
                for (int y = 1; y < 399; y++)
                {
                    // Approximate pressure gradient using central differences
                    float dpdx = (pressure[x + 1, y] - pressure[x - 1, y]) * 0.5f;
                    float dpdy = (pressure[x, y + 1] - pressure[x, y - 1]) * 0.5f;

                    velocities[x, y] = velocities[x, y] - new Vector2(dpdx, dpdy);
                }
            }
        }

        public static void InitialiseVelocities()
        {
            for (int i = 0; i < 400; i++)
            {
                for (int j = 0; j < 400; j++)
                {
                    velocities[i, j] = new Vector2(0, 0);
                    pressure[i, j] = 0f;
                }
            }
        }
        public static void ApplyMouseAcceleration(int x, int y, Vector2 delta, float deltaTime)
        {
            Vector2 currentPos = new Vector2(x, y);
            Vector2 velocity = new Vector2(delta.X / deltaTime, delta.Y / deltaTime);

            int radius = 2;
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

        static void Diffusion(float t)
        {
            velocities = JacobiDiffusionSolver(velocities, velocities, 30, 0.8f);

        }
        static Vector2[,] JacobiDiffusionSolver(Vector2[,] sourceGrid, Vector2[,] inputGrid, int iterations, float k)
        {
            int width = sourceGrid.GetLength(0);
            int height = sourceGrid.GetLength(1);

            Vector2[,] input = inputGrid;
            Vector2[,] output = new Vector2[width, height];

            for (int iter = 0; iter < iterations; iter++)
            {
                for (int x = 1; x < width - 1; x++)
                {
                    for (int y = 1; y < height - 1; y++)
                    {
                        output[x, y] = (sourceGrid[x, y] + (k / 4f) * SumOfNeighbours(input, x, y)) / (1 + k);
                    }
                }

                // Handle boundaries here, e.g. copy edges from input or set to fixed values
                for (int x = 0; x < width; x++)
                {
                    output[x, 0] = input[x, 0];
                    output[x, height - 1] = input[x, height - 1];
                }
                for (int y = 0; y < height; y++)
                {
                    output[0, y] = input[0, y];
                    output[width - 1, y] = input[width - 1, y];
                }

                // Swap input and output references
                var temp = input;
                input = output;
                output = temp;
            }

            // After last iteration, 'input' holds the latest values
            return input;
        }

        static Vector2 SumOfNeighbours(Vector2[,] grid, int x, int y)
        {
            return grid[x + 1, y] + grid[x - 1, y] + grid[x, y + 1] + grid[x, y - 1];
        }
        static void Advection(float t)
        {
            int width = 400;
            int height = 400;
            float damping = 0.98f;

            Vector2[,] newVelocities = new Vector2[width, height];

            Parallel.For(0, height, y =>
            {
                for (int x = 0; x < width; x++)
                {
                    // Backtrace position
                    Vector2 v = velocities[x, y];
                    Vector2 pos = new Vector2(x, y) - v * t;

                    // Clamp to valid range (so we don't read out of bounds)
                    pos.X = Math.Clamp(pos.X, 0, width - 1.001f);
                    pos.Y = Math.Clamp(pos.Y, 0, height - 1.001f);

                    // Bilinear interpolation
                    int x0 = (int)Math.Floor(pos.X);
                    int y0 = (int)Math.Floor(pos.Y);
                    int x1 = x0 + 1;
                    int y1 = y0 + 1;

                    float sx = pos.X - x0;
                    float sy = pos.Y - y0;

                    // Clamp x1, y1 for safety
                    if (x1 >= width) x1 = width - 1;
                    if (y1 >= height) y1 = height - 1;

                    Vector2 v00 = velocities[x0, y0];
                    Vector2 v10 = velocities[x1, y0];
                    Vector2 v01 = velocities[x0, y1];
                    Vector2 v11 = velocities[x1, y1];

                    Vector2 vx0 = v00 * (1 - sx) + v10 * sx;
                    Vector2 vx1 = v01 * (1 - sx) + v11 * sx;

                    Vector2 result = (vx0 * (1 - sy) + vx1 * sy) * damping;

                    newVelocities[x, y] = result;
                }
            });

            // Copy results back
            Parallel.For(0, height, y =>
            {
                for (int x = 0; x < width; x++)
                {
                    velocities[x, y] = newVelocities[x, y];
                }
            });
        }
        public static void EnforceVelocityBoundary()
        {
            int width = velocities.GetLength(0);
            int height = velocities.GetLength(1);

            for (int x = 0; x < width; x++)
            {
                velocities[x, 0] = Vector2.Zero;           // Bottom row
                velocities[x, height - 1] = Vector2.Zero;  // Top row
            }

            for (int y = 0; y < height; y++)
            {
                velocities[0, y] = Vector2.Zero;           // Left column
                velocities[width - 1, y] = Vector2.Zero;   // Right column
            }
        }
        public static void EnforcePressureBoundary()
        {
            int width = pressure.GetLength(0);
            int height = pressure.GetLength(1);

            for (int x = 0; x < width; x++)
            {
                pressure[x, 0] = pressure[x, 1];               // Bottom row
                pressure[x, height - 1] = pressure[x, height - 2];  // Top row
            }

            for (int y = 0; y < height; y++)
            {
                pressure[0, y] = pressure[1, y];               // Left column
                pressure[width - 1, y] = pressure[width - 2, y];   // Right column
            }
        }

    }
}
