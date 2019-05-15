using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.Graphics;
using Android.Media;
using Android.Opengl;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Java.Nio;
using SINTEF.AutoActive.FileSystem;
using SINTEF.AutoActive.Plugins.ArchivePlugins.Video;
using SINTEF.AutoActive.UI.Droid.Video;
using Xamarin.Forms;

[assembly: Dependency(typeof(VideoLengthExtractorFactory))]
namespace SINTEF.AutoActive.UI.Droid.Video
{
    internal class VideoLengthExtractor : IVideoLengthExtractor
    {
        MediaExtractor extractor;
        MediaCodec decoder;

        internal  VideoLengthExtractor(System.IO.Stream stream)
        {
            extractor = new MediaExtractor();
            extractor.SetDataSource(new ReadSeekStreamMediaSource(stream));
            var format = SelectFirstVideoTrack() ?? throw new Exception("Stream has no video track");

            // Get the original video size
            var naturalWidth = format.GetInteger(MediaFormat.KeyWidth);
            var naturalHeight = format.GetInteger(MediaFormat.KeyHeight);




            decoder = MediaCodec.CreateDecoderByType(format.GetString(MediaFormat.KeyMime));

            ///Surface.



            //extractor.SeekTo(0, MediaExtractorSeekTo.)
            var info = new MediaCodec.BufferInfo();
            //info.

            //videoLengthExtractor.
        }

        MediaFormat SelectFirstVideoTrack()
        {
            for (var i = 0; i < extractor.TrackCount; i++)
            {
                var format = extractor.GetTrackFormat(i);
                var mime = format.GetString(MediaFormat.KeyMime);
                if (mime.StartsWith("video/"))
                {
                    extractor.SelectTrack(i);
                    return format;
                }
            }
            return null;
        }

        /* -- Public API -- */
        public Task<double> GetLengthAsync()
        {
            throw new NotImplementedException();
        }


        public Task<double> SeekToAsync(double time, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<double> SeekToAsync(double time)
        {
            return SeekToAsync(time, CancellationToken.None);
        }


        public Task<(uint width, uint height)> SetSizeAsync(uint width, uint height, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<(uint width, uint height)> SetSizeAsync(uint width, uint height)
        {
            return SetSizeAsync(width, height, CancellationToken.None);
        }

        Task<long> IVideoLengthExtractor.GetLengthAsync()
        {
            throw new NotImplementedException();
        }

        public Task<long> SeekToAsync(long time, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<long> SeekToAsync(long time)
        {
            throw new NotImplementedException();
        }
    }

    public class VideoLengthExtractorFactory : IVideoLengthExtractorFactory
    {
        public async Task<IVideoLengthExtractor> CreateVideoDecoder(IReadSeekStreamFactory file, string mime)
        {
            var stream = await file.GetReadStream();
            return new VideoLengthExtractor(stream);
        }
    }

    /* -- Helper classes -- */

    internal class ReadSeekStreamMediaSource : MediaDataSource
    {
        System.IO.Stream original;

        internal ReadSeekStreamMediaSource(System.IO.Stream stream)
        {
            original = stream;
            Size = stream.Length;
        }

        public override long Size { get; }

        public override void Close()
        {
            original.Close();
        }

        public override int ReadAt(long position, byte[] buffer, int offset, int size)
        {
            original.Position = position;
            return original.Read(buffer, offset, size);
        }
    }

    //internal class CodecOutputSurface : SurfaceTexture.IOnFrameAvailableListener
    //{
    //    int _width;
    //    int _height;
    //    EGLDisplay _EGLDisplay = EGL14.EglNoDisplay;
    //    EGLContext _EGLContext = EGL14.EglNoContext;
    //    EGLSurface _EGLSurface = EGL14.EglNoSurface;

    //    internal CodecOutputSurface(int width, int height)
    //    {
    //        if (width <= 0 && height <= 0)
    //        {
    //            throw new ArgumentException("width and height must be larger than zero");
    //        }
    //        _width = width;
    //        _height = height;

    //        EGLSetup();
    //        MakeCurrent();
    //    }

    //    void CheckEGLError(string message)
    //    {
    //        var error = EGL14.EglGetError();
    //        if (error != EGL14.EglSuccess)
    //        {
    //            throw new Exception(message + ": EGL error " + error);
    //        }
    //    }

    //    void EGLSetup()
    //    {
    //        _EGLDisplay = EGL14.EglGetDisplay(EGL14.EglDefaultDisplay);
    //        if (_EGLDisplay == EGL14.EglNoDisplay)
    //        {
    //            throw new Exception("unable to get EGL14 display");
    //        }
    //        var version = new int[2];
    //        if (!EGL14.EglInitialize(_EGLDisplay, version, 0, version, 1))
    //        {
    //            _EGLDisplay = EGL14.EglNoDisplay;
    //            throw new Exception("unable to initialize EGL14");
    //        }

    //        // Configure EGL for pbuffer and OpenGL ES 2.0, 24-bit RGB.
    //        var attribList = new [] {
    //                EGL14.EglRedSize, 8,
    //                EGL14.EglGreenSize, 8,
    //                EGL14.EglBlueSize, 8,
    //                EGL14.EglAlphaSize, 8,
    //                EGL14.EglRenderableType, EGL14.EglOpenglEs2Bit,
    //                EGL14.EglSurfaceType, EGL14.EglPbufferBit,
    //                EGL14.EglNone
    //        };
    //        var configs = new EGLConfig[1];
    //        var numConfigs = new int[1];
    //        if (!EGL14.EglChooseConfig(_EGLDisplay, attribList, 0, configs, 0, configs.Length, numConfigs, 0))
    //        {
    //            throw new Exception("unable to find RGB888+recordable ES2 EGL config");
    //        }

    //        // Configure context for OpenGL ES 2.0.
    //        var attrib_list = new [] {
    //                EGL14.EglContextClientVersion, 2,
    //                EGL14.EglNone
    //        };
    //        _EGLContext = EGL14.EglCreateContext(_EGLDisplay, configs[0], EGL14.EglNoContext, attrib_list, 0);
    //        CheckEGLError("EglCreateContext");
    //        if (_EGLContext == null)
    //        {
    //            throw new Exception("null context");
    //        }

    //        // Create a pbuffer surface.
    //        var surfaceAttribs = new [] {
    //                EGL14.EglWidth, _width,
    //                EGL14.EglHeight, _height,
    //                EGL14.EglNone
    //        };
    //        _EGLSurface = EGL14.EglCreatePbufferSurface(_EGLDisplay, configs[0], surfaceAttribs, 0);
    //        CheckEGLError("eglCreatePbufferSurface");
    //        if (_EGLSurface == null)
    //        {
    //            throw new Exception("null surface");
    //        }
    //    }

    //    void MakeCurrent()
    //    {
    //        if (!EGL14.EglMakeCurrent(_EGLDisplay, _EGLSurface, _EGLSurface, _EGLContext))
    //        {
    //            throw new Exception("EglMakeCurrent failed");
    //        }
    //    }

    //    /* --- Implement interface --- */

    //    public IntPtr Handle { get; }

    //    public void Dispose()
    //    {
    //        if (_EGLDisplay != EGL14.EglNoDisplay)
    //        {
    //            EGL14.EglDestroySurface(_EGLDisplay, _EGLSurface);
    //            EGL14.EglDestroyContext(_EGLDisplay, _EGLContext);
    //            //EGL14.EglReleaseThread(); // FIXME: do this
    //            EGL14.EglTerminate(_EGLDisplay);
    //        }
    //        _EGLDisplay = EGL14.EglNoDisplay;
    //        _EGLContext = EGL14.EglNoContext;
    //        _EGLSurface = EGL14.EglNoSurface;

    //        // Release m.surface
    //    }

    //    public void OnFrameAvailable(SurfaceTexture surfaceTexture)
    //    {
    //        throw new NotImplementedException();
    //    }
    //}

    //internal class TextureRenderer
    //{
    //    static readonly int FLOAT_SIZE_BYTES = 4;

    //    float[] _TriangleVerticesData =
    //    {
    //        // X, Y, Z, U, V
    //        -1.0f, -1.0f, 0, 0.0f, 0.0f,
    //         1.0f, -1.0f, 0, 1.0f, 0.0f,
    //        -1.0f,  1.0f, 0, 0.0f, 1.0f,
    //         1.0f,  1.0f, 0, 1.0f, 1.0f,
    //    };

    //    FloatBuffer _TriangleVertices;

    //    static readonly string VERTEX_SHADER =
    //            "uniform mat4 uMVPMatrix;\n" +
    //            "uniform mat4 uSTMatrix;\n" +
    //            "attribute vec4 aPosition;\n" +
    //            "attribute vec4 aTextureCoord;\n" +
    //            "varying vec2 vTextureCoord;\n" +
    //            "void main() {\n" +
    //            "    gl_Position = uMVPMatrix * aPosition;\n" +
    //            "    vTextureCoord = (uSTMatrix * aTextureCoord).xy;\n" +
    //            "}\n";

    //    static readonly string FRAGMENT_SHADER =
    //            "#extension GL_OES_EGL_image_external : require\n" +
    //            "precision mediump float;\n" +
    //            "varying vec2 vTextureCoord;\n" +
    //            "uniform samplerExternalOES sTexture;\n" +
    //            "void main() {\n" +
    //            "    gl_FragColor = texture2D(sTexture, vTextureCoord);\n" +
    //            "}\n";

    //    float[] _MVPMatrix = new float[16];
    //    float[] _STMatrix = new float[16];

    //    int _Program;
    //    int _TextureID = -1;
    //    int _MVPMatrixHandle;
    //    int _STMatrixHandle;
    //    int _PositionHandle;
    //    int _TextureHandle;

    //    internal TextureRenderer()
    //    {
    //        _TriangleVertices = ByteBuffer.AllocateDirect(_TriangleVerticesData.Length * FLOAT_SIZE_BYTES).Order(ByteOrder.NativeOrder()).AsFloatBuffer();
    //        _TriangleVertices.Put(_TriangleVerticesData).Position(0);

    //        Android.Opengl.Matrix.SetIdentityM(_STMatrix, 0);
    //    }

    //    int LoadShader(int shaderType, string source)
    //    {
    //        var shader = GLES20.GlCreateShader(shaderType);
    //        CheckGLError("glCreateShader type=" + shaderType);
    //        GLES20.GlShaderSource(shader, source);
    //        GLES20.GlCompileShader(shader);
    //        var compiled = new int[1];
    //        GLES20.GlGetShaderiv(shader, GLES20.GlCompileStatus, compiled, 0);
    //        if (compiled[0] == 0)
    //        {
    //            GLES20.GlDeleteShader(shader);
    //            shader = 0;
    //        }
    //        return shader;
    //    }

    //    int CreateProgram(string vertexSource, string fragmentSource)
    //    {
    //        var vertexShader = LoadShader(GLES20.GlVertexShader, vertexSource);
    //        if (vertexShader == 0)
    //        {
    //            return 0;
    //        }
    //        var pixelShader = LoadShader(GLES20.GlFragmentShader, fragmentSource);
    //        if (pixelShader == 0)
    //        {
    //            return 0;
    //        }
    //        var program = GLES20.GlCreateProgram();
    //        GLES20.GlAttachShader(program, vertexShader);
    //        CheckGLError("glAttachShader");
    //        GLES20.GlAttachShader(program, pixelShader);
    //        CheckGLError("glAttachShader");
    //        GLES20.GlLinkProgram(program);
    //        var linkStatus = new int[1];
    //        GLES20.GlGetProgramiv(program, GLES20.GlLinkStatus, linkStatus, 0);
    //        if (linkStatus[0] != GLES20.GlTrue)
    //        {
    //            GLES20.GlDeleteProgram(program);
    //            program = 0;
    //        }
    //        return program;
    //    }


    //    void CheckGLError(string message)
    //    {
    //        var error = GLES20.GlGetError();
    //        if (error != GLES20.GlNoError)
    //        {
    //            throw new Exception(message + ": GlError " + error);
    //        }
    //    }

    //    void CheckLocation(int location, string label)
    //    {
    //        if (location < 0)
    //        {
    //            throw new Exception("Unable to locate '" + label + "' in program");
    //        }
    //    }

    //    /* -- Public API -- */
    //    public int TextureID => _TextureID;

    //    public void SurfaceCreated()
    //    {
    //        _Program = CreateProgram(VERTEX_SHADER, FRAGMENT_SHADER);
    //        if (_Program == 0)
    //        {
    //            throw new Exception("failed creating program");
    //        }

    //        _PositionHandle = GLES20.GlGetAttribLocation(_Program, "aPosition");
    //        CheckLocation(_PositionHandle, "aPosition");
    //        _TextureHandle = GLES20.GlGetAttribLocation(_Program, "aTextureCoord");
    //        CheckLocation(_TextureHandle, "aTextureCoord");

    //        _MVPMatrixHandle = GLES20.GlGetUniformLocation(_Program, "uMVPMatrix");
    //        CheckLocation(_MVPMatrixHandle, "uMVPMatrix");
    //        _STMatrixHandle = GLES20.GlGetUniformLocation(_Program, "uSTMatrix");
    //        CheckLocation(_STMatrixHandle, "uSTMatrix");

    //        int[] textures = new int[1];
    //        GLES20.GlGenTextures(1, textures, 0);

    //        _TextureID = textures[0];
    //        GLES20.GlBindTexture(GLES11Ext.GlTextureExternalOes, _TextureID);
    //        CheckGLError("glBindTexture mTextureID");

    //        GLES20.GlTexParameterf(GLES11Ext.GlTextureExternalOes, GLES20.GlTextureMinFilter, GLES20.GlNearest);
    //        GLES20.GlTexParameterf(GLES11Ext.GlTextureExternalOes, GLES20.GlTextureMagFilter, GLES20.GlLinear);
    //        GLES20.GlTexParameterf(GLES11Ext.GlTextureExternalOes, GLES20.GlTextureWrapS, GLES20.GlClampToEdge);
    //        GLES20.GlTexParameterf(GLES11Ext.GlTextureExternalOes, GLES20.GlTextureWrapT, GLES20.GlClampToEdge);
    //        CheckGLError("glTexParameter");
    //    }
    //}

}