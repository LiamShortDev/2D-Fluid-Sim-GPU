using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;

namespace _2D_Fluid_Sim_GPU
{
    public class Game : GameWindow
    {
        private readonly float[] _quadVertices =
        {
            // positions     // texCoords
            -1.0f,  1.0f,    0.0f, 1.0f,
            -1.0f, -1.0f,    0.0f, 0.0f,
             1.0f, -1.0f,    1.0f, 0.0f,

            -1.0f,  1.0f,    0.0f, 1.0f,
             1.0f, -1.0f,    1.0f, 0.0f,
             1.0f,  1.0f,    1.0f, 1.0f
        };

        int width;
        int height;
        bool showArrows = false;
        int VBO, VAO;
        Shader renderShader;
        Shader arrowShader;
        Shader diffusionComputeShader;
        Shader pressureComputeShader;

        int velocityTexA, velocityTexB;
        int readTex, writeTex;

        int pressureTexA, pressureTexB, divergenceTex;
        float[] velocityData;
        float[] divergence;
        float[] pressure;

        // Arrow buffers
        int arrowVBO;
        int arrowVAO;

        // For toggling arrow display
        bool aPressedLastFrame = false;

        public Game(int windowWidth, int windowHeight, string title, int simWidth, int simHeight)
            : base(GameWindowSettings.Default, new NativeWindowSettings() { Size = (windowWidth, windowHeight), Title = title })
        {
            width = simWidth;
            height = simHeight;
        }

        // Map simulation coordinate [0..maxCoord-1] to NDC [-1..1]
        float ToNDC(int coord, int maxCoord) => (coord / (float)(maxCoord - 1)) * 2.0f - 1.0f;

        protected override void OnLoad()
        {
            base.OnLoad();

            GL.ClearColor(0.2f, 0.3f, 0.3f, 1.0f);

            // Setup VAO + VBO for fullscreen quad
            VAO = GL.GenVertexArray();
            GL.BindVertexArray(VAO);

            VBO = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ArrayBuffer, VBO);
            GL.BufferData(BufferTarget.ArrayBuffer, _quadVertices.Length * sizeof(float), _quadVertices, BufferUsageHint.StaticDraw);

            GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), 0);
            GL.EnableVertexAttribArray(0);

            GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), 2 * sizeof(float));
            GL.EnableVertexAttribArray(1);

            // Load shaders
            renderShader = new Shader("shader.vert", "shader.frag");
            arrowShader = new Shader("arrow.vert", "arrow.frag");
            diffusionComputeShader = new Shader("diffusion.comp");
            pressureComputeShader = new Shader("pressure.comp");

            // Create simulation textures
            readTex = CreateVelocityTexture(width, height);
            writeTex = CreateVelocityTexture(width, height);

            // Initialize simulation data (all zeros here)
            velocityData = new float[width * height * 2];
            GL.BindTexture(TextureTarget.Texture2D, readTex);
            GL.TexSubImage2D(TextureTarget.Texture2D, 0, 0, 0, width, height,
                             PixelFormat.Rg, PixelType.Float, velocityData);
            GL.BindTexture(TextureTarget.Texture2D, 0);

            pressureTexA = CreatePressureTexture(width, height);
            pressureTexB = CreatePressureTexture(width, height);

            pressure = new float[width * height];
            GL.BindTexture(TextureTarget.Texture2D, pressureTexA);
            GL.TexSubImage2D(TextureTarget.Texture2D, 0, 0, 0, width, height,
                             PixelFormat.Red, PixelType.Float, pressure);
            GL.BindTexture(TextureTarget.Texture2D, 0);

            divergenceTex = CreatePressureTexture(width, height);

            divergence = new float[width * height];
            GL.BindTexture(TextureTarget.Texture2D, divergenceTex);
            GL.TexSubImage2D(TextureTarget.Texture2D, 0, 0, 0, width, height,
                             PixelFormat.Red, PixelType.Float, divergence);
            GL.BindTexture(TextureTarget.Texture2D, 0);

            Sim.InitialiseVelocities();
            UploadVelocitiesToTexture(Sim.GetVelocities(), readTex);
            Sim.UpdateSimulation((float)UpdateTime);

            SetupArrowBuffers();
        }

        private int CreateVelocityTexture(int w, int h)
        {
            int tex = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, tex);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rg32f,
                          w, h, 0, PixelFormat.Rg, PixelType.Float, IntPtr.Zero);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
            GL.BindTexture(TextureTarget.Texture2D, 0);
            return tex;
        }

        private int CreatePressureTexture(int w, int h)
        {
            int tex = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, tex);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.R32f,
                          w, h, 0, PixelFormat.Red, PixelType.Float, IntPtr.Zero);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
            GL.BindTexture(TextureTarget.Texture2D, 0);
            return tex;
        }

        protected override void OnUpdateFrame(FrameEventArgs args)
        {
            base.OnUpdateFrame(args);

            if (KeyboardState.IsKeyDown(Keys.Escape))
                Close();

            if (KeyboardState.IsKeyDown(Keys.A) && !aPressedLastFrame)
            {
                showArrows = !showArrows;
            }
            aPressedLastFrame = KeyboardState.IsKeyDown(Keys.A);

            float k = 0.8f;
            int iterations = 40;
            for (int i = 0; i < iterations; i++)
            {
                RunJacobiIteration(k);
            }

            ComputeDivergence();
            UploadDivergenceToTexture(divergence, divergenceTex, width, height);

            iterations = 50;
            for (int i = 0; i < iterations; i++)
            {
                RunPressureJacobiIteration();
            }

            ReadTextureToVelocities(readTex, width, height);
            Sim.SetPressure(ConvertPressureTextureToArray(pressureTexA, width, height), width, height);
            Sim.UpdateSimulation((float)UpdateTime);
            UploadVelocitiesToTexture(Sim.GetVelocities(), readTex);
        }

        private void UploadVelocitiesToTexture(Vector2[,] velocities, int textureID)
        {
            int w = velocities.GetLength(0);
            int h = velocities.GetLength(1);

            float[] flatData = FlattenVelocityArray(velocities);

            GL.BindTexture(TextureTarget.Texture2D, textureID);
            GL.TexSubImage2D(TextureTarget.Texture2D,
                0, 0, 0, w, h,
                PixelFormat.Rg,
                PixelType.Float,
                flatData);
            GL.BindTexture(TextureTarget.Texture2D, 0);
        }

        private void ReadTextureToVelocities(int textureId, int w, int h)
        {
            float[] texData = new float[w * h * 2]; // RG floats

            GL.BindTexture(TextureTarget.Texture2D, textureId);
            GL.GetTexImage(TextureTarget.Texture2D, 0, PixelFormat.Rg, PixelType.Float, texData);
            GL.BindTexture(TextureTarget.Texture2D, 0);

            Vector2[,] velocities = new Vector2[w, h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int index = (y * w + x) * 2;
                    velocities[x, y] = new Vector2(texData[index], texData[index + 1]);
                }
            }
            Sim.SetVelocities(velocities);
        }

        void RunJacobiIteration(float k)
        {
            diffusionComputeShader.Use();

            GL.BindImageTexture(0, readTex, 0, false, 0, TextureAccess.ReadOnly, SizedInternalFormat.Rg32f);
            GL.BindImageTexture(1, writeTex, 0, false, 0, TextureAccess.WriteOnly, SizedInternalFormat.Rg32f);

            int loc = GL.GetUniformLocation(diffusionComputeShader.Handle, "k");
            if (loc >= 0)
                GL.Uniform1(loc, k);

            int groupSize = 16;
            int groupsX = width / groupSize;
            int groupsY = height / groupSize;

            GL.DispatchCompute(groupsX, groupsY, 1);
            GL.MemoryBarrier(MemoryBarrierFlags.ShaderImageAccessBarrierBit);

            (readTex, writeTex) = (writeTex, readTex);
        }

        private void ComputeDivergence()
        {
            Vector2[,] velocities = Sim.GetVelocities();

            for (int x = 1; x < width - 1; x++)
            {
                for (int y = 1; y < height - 1; y++)
                {
                    int index = y * width + x;
                    float dpdx = (velocities[x + 1, y].X - velocities[x - 1, y].X) * 0.5f;
                    float dpdy = (velocities[x, y + 1].Y - velocities[x, y - 1].Y) * 0.5f;
                    divergence[index] = dpdx + dpdy;
                }
            }
        }

        private void UploadDivergenceToTexture(float[] divergenceData, int texID, int w, int h)
        {
            GL.BindTexture(TextureTarget.Texture2D, texID);
            GL.TexSubImage2D(TextureTarget.Texture2D, 0, 0, 0, w, h, PixelFormat.Red, PixelType.Float, divergenceData);
            GL.BindTexture(TextureTarget.Texture2D, 0);
        }

        void RunPressureJacobiIteration()
        {
            pressureComputeShader.Use();
            GL.BindImageTexture(0, pressureTexA, 0, false, 0, TextureAccess.ReadOnly, SizedInternalFormat.R32f);
            GL.BindImageTexture(1, divergenceTex, 0, false, 0, TextureAccess.ReadOnly, SizedInternalFormat.R32f);
            GL.BindImageTexture(2, pressureTexB, 0, false, 0, TextureAccess.WriteOnly, SizedInternalFormat.R32f);

            int groupSize = 16;
            int groupsX = width / groupSize;
            int groupsY = height / groupSize;

            GL.DispatchCompute(groupsX, groupsY, 1);
            GL.MemoryBarrier(MemoryBarrierFlags.ShaderImageAccessBarrierBit);

            (pressureTexA, pressureTexB) = (pressureTexB, pressureTexA);
        }

        private float[] ConvertPressureTextureToArray(int pressureTextureID, int w, int h)
        {
            float[] pressureData = new float[w * h];

            GL.BindTexture(TextureTarget.Texture2D, pressureTextureID);
            GL.GetTexImage(TextureTarget.Texture2D, 0, PixelFormat.Red, PixelType.Float, pressureData);
            GL.BindTexture(TextureTarget.Texture2D, 0);

            return pressureData;
        }

        protected override void OnRenderFrame(FrameEventArgs args)
        {
            base.OnRenderFrame(args);

            GL.Clear(ClearBufferMask.ColorBufferBit);

            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, readTex);

            renderShader.Use();
            GL.Uniform1(GL.GetUniformLocation(renderShader.Handle, "velocityTex"), 0);

            GL.BindVertexArray(VAO);
            GL.DrawArrays(PrimitiveType.Triangles, 0, 6);

            if (showArrows)
            {
                float[] arrowVertices = GenerateArrowVertices(Sim.GetVelocities(), width, height);

                GL.BindBuffer(BufferTarget.ArrayBuffer, arrowVBO);
                GL.BufferData(BufferTarget.ArrayBuffer, arrowVertices.Length * sizeof(float), arrowVertices, BufferUsageHint.DynamicDraw);

                GL.BindVertexArray(arrowVAO);
                arrowShader.Use();

                GL.DrawArrays(PrimitiveType.Lines, 0, arrowVertices.Length / 3);

                GL.BindVertexArray(0);
            }


            SwapBuffers();
        }

        private float[] FlattenVelocityArray(Vector2[,] simVelocities)
        {
            int w = simVelocities.GetLength(0);
            int h = simVelocities.GetLength(1);
            float[] velocityData = new float[w * h * 2];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int index = (y * w + x) * 2;
                    velocityData[index] = simVelocities[x, y].X;
                    velocityData[index + 1] = simVelocities[x, y].Y;
                }
            }
            return velocityData;
        }

        protected override void OnMouseMove(MouseMoveEventArgs e)
        {
            base.OnMouseMove(e);

            if (MouseState.IsButtonDown(MouseButton.Left))
            {
                // Convert mouse pixel coords to sim grid coords
                int simX = (int)(e.Position.X / Size.X * width);
                int simY = (int)((Size.Y - e.Position.Y) / Size.Y * height);

                Sim.ApplyMouseAcceleration(simX, simY, e.Delta, (float)UpdateTime);
                UploadVelocitiesToTexture(Sim.GetVelocities(), readTex);
            }
        }

        private float[] GenerateArrowVertices(Vector2[,] velocities, int w, int h, float arrowScale = 0.02f)
        {
            List<float> vertices = new List<float>();

            for (int y = 0; y < h; y += 10)
            {
                for (int x = 0; x < w; x += 10)
                {
                    Vector2 v = velocities[x, y];
                    float startX = ToNDC(x, w);
                    float startY = ToNDC(y, h);

                    float mag = v.Length;
                    if (mag < 0.01f) continue;

                    Vector2 dir = v.Normalized();

                    float length = MathF.Min(mag * arrowScale, arrowScale * 5);
                    length = arrowScale;
                    float endX = startX + dir.X * length;
                    float endY = startY + dir.Y * length;

                    vertices.AddRange(new float[] { startX, startY, mag, endX, endY, mag });
                }
            }

            return vertices.ToArray();
        }

        void SetupArrowBuffers()
        {
            arrowVAO = GL.GenVertexArray();
            GL.BindVertexArray(arrowVAO);

            arrowVBO = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ArrayBuffer, arrowVBO);

            // Initialize with zero size (or small size) but expect 3 floats per vertex now
            GL.BufferData(BufferTarget.ArrayBuffer, 0, IntPtr.Zero, BufferUsageHint.DynamicDraw);

            // Position attribute (location = 0): 2 floats, offset 0, stride 3 floats
            GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 3 * sizeof(float), 0);
            GL.EnableVertexAttribArray(0);

            // Magnitude attribute (location = 1): 1 float, offset 2 floats
            GL.VertexAttribPointer(1, 1, VertexAttribPointerType.Float, false, 3 * sizeof(float), 2 * sizeof(float));
            GL.EnableVertexAttribArray(1);

            GL.BindVertexArray(0);
        }


        protected override void OnUnload()
        {
            base.OnUnload();

            GL.DeleteBuffer(VBO);
            GL.DeleteVertexArray(VAO);
            GL.DeleteProgram(renderShader.Handle);
            GL.DeleteProgram(arrowShader.Handle);
            GL.DeleteProgram(diffusionComputeShader.Handle);
            GL.DeleteProgram(pressureComputeShader.Handle);
            GL.DeleteTexture(readTex);
            GL.DeleteTexture(writeTex);
            GL.DeleteTexture(pressureTexA);
            GL.DeleteTexture(pressureTexB);
            GL.DeleteTexture(divergenceTex);
            GL.DeleteBuffer(arrowVBO);
            GL.DeleteVertexArray(arrowVAO);
        }
    }
}
