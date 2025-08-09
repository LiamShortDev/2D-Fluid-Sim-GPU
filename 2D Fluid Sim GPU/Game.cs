using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using OpenTK.Mathematics;
using System.Diagnostics;


namespace _2D_Fluid_Sim_GPU
{
    public class Game : GameWindow
    {
        private readonly float[] _quadVertices =
        {
            // positions     // texCoords
            -1.0f,  1.0f,    0.0f, 1.0f, // top-left
            -1.0f, -1.0f,    0.0f, 0.0f, // bottom-left
             1.0f, -1.0f,    1.0f, 0.0f, // bottom-right

            -1.0f,  1.0f,    0.0f, 1.0f, // top-left
             1.0f, -1.0f,    1.0f, 0.0f, // bottom-right
             1.0f,  1.0f,    1.0f, 1.0f  // top-right
        };
        int VertexBufferObject;
        int ElementBufferObject;
        int VertexArrayObject;
        Shader shader;

        float[] velocityData;
        int velocityTexture;
        public Game(int width, int height, string title) : base(GameWindowSettings.Default, new NativeWindowSettings() { Size = (width, height), Title = title }) { }
        protected override void OnUpdateFrame(FrameEventArgs args)
        {
            base.OnUpdateFrame(args);

            if (KeyboardState.IsKeyDown(Keys.Escape))
            {
                Close();
            }
        }
        protected override void OnLoad()
        {
            base.OnLoad();

            GL.ClearColor(0.2f, 0.3f, 0.3f, 1.0f);

            // Create VAO + VBO
            VertexArrayObject = GL.GenVertexArray();
            GL.BindVertexArray(VertexArrayObject);

            VertexBufferObject = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ArrayBuffer, VertexBufferObject);
            GL.BufferData(BufferTarget.ArrayBuffer, _quadVertices.Length * sizeof(float), _quadVertices, BufferUsageHint.StaticDraw);

            // Position attribute
            GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), 0);
            GL.EnableVertexAttribArray(0);

            // TexCoord attribute
            GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), 2 * sizeof(float));
            GL.EnableVertexAttribArray(1);

            // Load shaders
            shader = new Shader("shader.vert", "shader.frag");
            shader.Use();

            // Create velocity texture
            int simWidth = 400, simHeight = 400;
            velocityTexture = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, velocityTexture);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rg32f,
                          simWidth, simHeight, 0, PixelFormat.Rg, PixelType.Float, IntPtr.Zero);

            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);

            // Set sampler uniform
            GL.Uniform1(GL.GetUniformLocation(shader.Handle, "velocityTex"), 0);

            Sim.InitialiseVelocities();
            FlattenVelocityArray(Sim.GetVelocities());
        }

        private void FlattenVelocityArray(Vector2[,] simVelocities)
        {
            velocityData = new float[simVelocities.GetLength(0)*simVelocities.GetLength(1) * 2];
            for (int y = 0; y < 400; y++)
            {
                for (int x = 0; x < 400; x++)
                {
                    int index = (y * 400 + x) * 2;
                    velocityData[index] = (float)simVelocities[x, y].X;
                    velocityData[index + 1] = (float)simVelocities[x, y].Y;
                }
            }
        }
        protected override void OnMouseMove(MouseMoveEventArgs e)
        {
            base.OnMouseMove(e);
            if (MouseState.IsButtonDown(MouseButton.Left))
            {
                // Convert mouse pixel coordinates to simulation grid coords if needed
                int simX = (int)(e.Position.X / Size.X * 400);
                int simY = (int)((Size.Y - e.Position.Y) / Size.Y * 400); // flip Y if needed

                // Pass to sim
                Sim.ApplyMouseAcceleration(simX, simY, e.Delta, (float)UpdateTime);
            }
        }


        protected override void OnRenderFrame(FrameEventArgs args)
        {
            base.OnRenderFrame(args);

            GL.Clear(ClearBufferMask.ColorBufferBit);

            // Update velocity texture (replace with your fluid sim’s data)
            GL.BindTexture(TextureTarget.Texture2D, velocityTexture);
            GL.TexSubImage2D(TextureTarget.Texture2D, 0, 0, 0, 400, 400,
                             PixelFormat.Rg, PixelType.Float, velocityData);

            shader.Use();
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, velocityTexture);

            GL.BindVertexArray(VertexArrayObject);
            GL.DrawArrays(PrimitiveType.Triangles, 0, 6);

            SwapBuffers();
            Sim.UpdateSimulation((float)UpdateTime);
            FlattenVelocityArray(Sim.GetVelocities());
        }


        protected override void OnFramebufferResize(FramebufferResizeEventArgs e)
        {
            base.OnFramebufferResize(e);

            GL.Viewport(0, 0, e.Width, e.Height);
        }

        protected override void OnUnload()
        {
            base.OnUnload();

            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            GL.BindVertexArray(0);
            GL.UseProgram(0);

            // Delete all the resources.
            GL.DeleteBuffer(VertexBufferObject);
            GL.DeleteVertexArray(VertexArrayObject);

            GL.DeleteProgram(shader.Handle);
        }
    }
}
