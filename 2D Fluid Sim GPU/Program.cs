using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Graphics.OpenGL4;

namespace _2D_Fluid_Sim_GPU
{
    class Program
    {
        static void Main()
        {
            using (Game game = new Game(800, 800, "2D Fluid Sim", 400, 400))
            {
                game.Run();
            }
        }
    }
}
