using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using SharpDX.Mathematics.Interop;
using Device = SharpDX.Direct3D11.Device;

namespace DesktopDuplication.Port
{
    public class ThreadManager : IDisposable
    {
        readonly List<Task> _threads = new List<Task>();
        readonly List<ThreadData> _threadData = new List<ThreadData>();

        public PointerInfo PointerInfo { get; } = new PointerInfo();

        public void Dispose()
        {
            if (PointerInfo.PtrShapeBuffer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(PointerInfo.PtrShapeBuffer);
                PointerInfo.PtrShapeBuffer = IntPtr.Zero;
            }

            foreach (var threadData in _threadData)
            {
                CleanDx(threadData.DxResources);
            }
        }

        public void Initialize(int SingleOutput, int OutputCount, IntPtr SharedHandle, RawRectangle DesktopDim)
        {
            for (var i = 0; i < OutputCount; i++)
            {
                var threadData = new ThreadData
                {
                    Output = SingleOutput < 0 ? i : SingleOutput,
                    TexSharedHandle = SharedHandle,
                    OffsetX = DesktopDim.Left,
                    OffsetY = DesktopDim.Top,
                    PointerInfo = PointerInfo,
                    DxResources = new DxResources()
                };

                InitializeDx(threadData.DxResources);

                _threadData.Add(threadData);

                _threads.Add(Task.Run(() => DdProc(threadData)));
            }
        }

        public void InitializeDx(DxResources Data)
        {
            var driverTypes = new[]
            {
                DriverType.Hardware,
                DriverType.Warp,
                DriverType.Reference
            };

            var featureLevels = new[]
            {
                FeatureLevel.Level_11_0,
                FeatureLevel.Level_10_1,
                FeatureLevel.Level_10_0,
                FeatureLevel.Level_9_1
            };

            foreach (var driverType in driverTypes)
            {
                try
                {
                    Data.Device = new Device(driverType, 0, featureLevels);

                    // Device creation success, no need to loop anymore
                    break;
                }
                catch
                {
                    // try again
                }
            }

            if (Data.Device == null)
                throw new Exception("Device creation failed");

            // TODO: Implement Vertex Shader Load
            byte[] vertexShaderBytecode = null;

            Data.VertexShader = new VertexShader(Data.Device, vertexShaderBytecode);

            var layout = new[]
            {
                new InputElement("POSITION", 0, Format.R32G32B32_Float, 0, 0, InputClassification.PerVertexData, 0), 
                new InputElement("TEXCOORD", 0, Format.R32G32_Float, 0, 12, InputClassification.PerVertexData, 0), 
            };

            Data.InputLayout = new InputLayout(Data.Device, vertexShaderBytecode, layout);

            Data.Device.ImmediateContext.InputAssembler.InputLayout = Data.InputLayout;

            // TODO: Implement Pixel Shader Load
            byte[] pixelShaderBytecode = null;

            Data.PixelShader = new PixelShader(Data.Device, pixelShaderBytecode);

            var samplerDesc = new SamplerStateDescription
            {
                Filter = Filter.MinMagMipLinear,
                AddressU = TextureAddressMode.Clamp,
                AddressV = TextureAddressMode.Clamp,
                AddressW = TextureAddressMode.Clamp,
                ComparisonFunction = Comparison.Never,
                MinimumLod = 0,
                MaximumLod = float.MaxValue
            };

            Data.SamplerLinear = new SamplerState(Data.Device, samplerDesc);
        }

        void DdProc(ThreadData Data) { }

        void CleanDx(DxResources Data)
        {
            Data.Device.Dispose();
            Data.VertexShader.Dispose();
            Data.PixelShader.Dispose();
            Data.InputLayout.Dispose();
            Data.SamplerLinear.Dispose();
        }

        public async Task WaitAll()
        {
            await Task.WhenAll(_threads);
        }
    }
}