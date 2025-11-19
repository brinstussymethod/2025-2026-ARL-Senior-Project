using System.IO;
using OpenTK.Graphics.OpenGL4;
using StbImageSharp;

namespace UnBox3D.Rendering.OpenGL
{
    /// <summary>
    /// Represents an OpenGL texture loaded from an image file.
    /// Handles loading the image, creating the OpenGL texture object,
    /// setting texture parameters, and binding the texture for use in rendering.
    /// </summary>
    public class Texture : IDisposable
    {
        public int Handle { get; private set; }
        public string FilePath { get; private set; }

        public Texture(string path)
        {
            FilePath = path;

            if (!File.Exists(path))
                throw new FileNotFoundException($"Texture file not found: {path}");

            // STBI loads images flipped by default, but OpenGL expects 0,0 to be bottom-left.
            StbImage.stbi_set_flip_vertically_on_load(1);

            using var stream = File.OpenRead(path);
            var image = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);

            // Generate OpenGL texture object
            Handle = GL.GenTexture();
            Bind(TextureUnit.Texture0);

            // Upload pixel data
            GL.TexImage2D(
                TextureTarget.Texture2D,
                level: 0,
                internalformat: PixelInternalFormat.Rgba,
                width: image.Width,
                height: image.Height,
                border: 0,
                format: PixelFormat.Rgba,
                type: PixelType.UnsignedByte,
                pixels: image.Data
            );

            // Generate mipmaps
            GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);

            // Texture parameters (standard)
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.LinearMipmapLinear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);

            // Wrapping
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);
        }

        public void Bind(TextureUnit unit)
        {
            GL.ActiveTexture(unit);
            GL.BindTexture(TextureTarget.Texture2D, Handle);
        }

        public void Dispose()
        {
            if (Handle != 0)
            {
                GL.DeleteTexture(Handle);
                Handle = 0;
            }
        }
    }
}
