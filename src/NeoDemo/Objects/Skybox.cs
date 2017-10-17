﻿using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Advanced;
using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using Vd2.Utilities;

namespace Vd2.NeoDemo.Objects
{
    public class Skybox : Renderable
    {
        private readonly Image<Rgba32> _front;
        private readonly Image<Rgba32> _back;
        private readonly Image<Rgba32> _left;
        private readonly Image<Rgba32> _right;
        private readonly Image<Rgba32> _top;
        private readonly Image<Rgba32> _bottom;

        // Context objects
        private VertexBuffer _vb;
        private IndexBuffer _ib;
        private UniformBuffer _viewMatrixBuffer;
        private TextureCube _cubemapTexture;
        private TextureView _cubemapBinding;
        private Pipeline _pipeline;
        private ResourceSet _resourceSet;

        public Skybox(
            Image<Rgba32> front, Image<Rgba32> back, Image<Rgba32> left,
            Image<Rgba32> right, Image<Rgba32> top, Image<Rgba32> bottom)
        {
            _front = front;
            _back = back;
            _left = left;
            _right = right;
            _top = top;
            _bottom = bottom;
        }

        public unsafe override void CreateDeviceObjects(GraphicsDevice gd, CommandList cl, SceneContext sc)
        {
            ResourceFactory factory = gd.ResourceFactory;

            _vb = factory.CreateVertexBuffer(new BufferDescription(s_vertices.SizeInBytes()));
            cl.UpdateBuffer(_vb, 0, s_vertices);

            _ib = factory.CreateIndexBuffer(new IndexBufferDescription(s_indices.SizeInBytes(), IndexFormat.UInt16));
            cl.UpdateBuffer(_ib, 0, s_indices);

            _viewMatrixBuffer = factory.CreateUniformBuffer(new BufferDescription((ulong)Unsafe.SizeOf<Matrix4x4>()));

            fixed (Rgba32* frontPin = &_front.DangerousGetPinnableReferenceToPixelBuffer())
            fixed (Rgba32* backPin = &_back.DangerousGetPinnableReferenceToPixelBuffer())
            fixed (Rgba32* leftPin = &_left.DangerousGetPinnableReferenceToPixelBuffer())
            fixed (Rgba32* rightPin = &_right.DangerousGetPinnableReferenceToPixelBuffer())
            fixed (Rgba32* topPin = &_top.DangerousGetPinnableReferenceToPixelBuffer())
            fixed (Rgba32* bottomPin = &_bottom.DangerousGetPinnableReferenceToPixelBuffer())
            {
                uint width = (uint)_front.Width;
                uint height = (uint)_front.Height;
                _cubemapTexture = factory.CreateTextureCube(new TextureDescription(
                    width,
                    height,
                    1,
                    1,
                    PixelFormat.R8_G8_B8_A8_UNorm,
                    TextureUsage.Sampled));

                uint faceSize = (uint)(_front.Width * _front.Height * Unsafe.SizeOf<Rgba32>());
                cl.UpdateTextureCube(_cubemapTexture, (IntPtr)leftPin, faceSize, CubeFace.NegativeX, 0, 0, width, height, 0, 0);
                cl.UpdateTextureCube(_cubemapTexture, (IntPtr)rightPin, faceSize, CubeFace.PositiveX, 0, 0, width, height, 0, 0);
                cl.UpdateTextureCube(_cubemapTexture, (IntPtr)bottomPin, faceSize, CubeFace.NegativeY, 0, 0, width, height, 0, 0);
                cl.UpdateTextureCube(_cubemapTexture, (IntPtr)topPin, faceSize, CubeFace.PositiveY, 0, 0, width, height, 0, 0);
                cl.UpdateTextureCube(_cubemapTexture, (IntPtr)backPin, faceSize, CubeFace.NegativeZ, 0, 0, width, height, 0, 0);
                cl.UpdateTextureCube(_cubemapTexture, (IntPtr)frontPin, faceSize, CubeFace.PositiveZ, 0, 0, width, height, 0, 0);

                _cubemapBinding = factory.CreateTextureView(new TextureViewDescription(_cubemapTexture));
            }

            VertexLayoutDescription[] vertexLayouts = new VertexLayoutDescription[]
            {
                new VertexLayoutDescription(
                    new VertexElementDescription("Position", VertexElementFormat.Float3, VertexElementSemantic.Position))
            };

            ShaderStageDescription[] shaderStages = new ShaderStageDescription[]
            {
                new ShaderStageDescription(ShaderStages.Vertex, ShaderHelper.LoadShader(factory, "Skybox", ShaderStages.Vertex), "VS"),
                new ShaderStageDescription(ShaderStages.Fragment, ShaderHelper.LoadShader(factory, "Skybox", ShaderStages.Fragment), "VS"),
            };

            PipelineDescription pd = new PipelineDescription(
                BlendStateDescription.SingleAlphaBlend,
                DepthStencilStateDescription.LessEqual,
                new RasterizerStateDescription(FaceCullMode.None, TriangleFillMode.Solid, false, true),
                PrimitiveTopology.TriangleList,
                new ShaderSetDescription(vertexLayouts, shaderStages));

            _pipeline = factory.CreatePipeline(ref pd);

            ResourceLayout layout = factory.CreateResourceLayout(new ResourceLayoutDescription(
                new ResourceLayoutElementDescription("Projection", ResourceKind.Uniform, ShaderStages.Vertex),
                new ResourceLayoutElementDescription("View", ResourceKind.Uniform, ShaderStages.Vertex),
                new ResourceLayoutElementDescription("CubeTexture", ResourceKind.TextureCube, ShaderStages.Fragment),
                new ResourceLayoutElementDescription("CubeSampler", ResourceKind.Sampler, ShaderStages.Fragment)));

            _resourceSet = factory.CreateResourceSet(new ResourceSetDescription(
                layout,
                sc.ProjectionMatrixBuffer,
                _viewMatrixBuffer,
                _cubemapBinding,
                gd.PointSampler));
        }

        public static Skybox LoadDefaultSkybox()
        {
            return new Skybox(
                Image.Load(AssetHelper.GetPath("Textures/cloudtop/cloudtop_ft.png")),
                Image.Load(AssetHelper.GetPath("Textures/cloudtop/cloudtop_bk.png")),
                Image.Load(AssetHelper.GetPath("Textures/cloudtop/cloudtop_lf.png")),
                Image.Load(AssetHelper.GetPath("Textures/cloudtop/cloudtop_rt.png")),
                Image.Load(AssetHelper.GetPath("Textures/cloudtop/cloudtop_up.png")),
                Image.Load(AssetHelper.GetPath("Textures/cloudtop/cloudtop_dn.png")));
        }

        public override void DestroyDeviceObjects()
        {
            _cubemapTexture.Dispose();
            _cubemapBinding.Dispose();
            _vb.Dispose();
            _ib.Dispose();
            _viewMatrixBuffer.Dispose();
        }

        public override void Render(GraphicsDevice gd, CommandList cl, SceneContext sc, RenderPasses renderPass)
        {
            cl.SetVertexBuffer(0, _vb, VertexPosition.SizeInBytes);
            cl.SetIndexBuffer(_ib);
            cl.SetPipeline(_pipeline);
            cl.SetResourceSet(_resourceSet);
            Matrix4x4 viewMat = ConvertToMatrix3x3(sc.Camera.ViewMatrix);
            cl.UpdateBuffer(_viewMatrixBuffer, 0, ref viewMat);
            cl.Draw((uint)s_indices.Length, 1, 0, 0, 0);
        }

        private static Matrix4x4 ConvertToMatrix3x3(Matrix4x4 m)
        {
            return new Matrix4x4(
                m.M11, m.M12, m.M13, 0,
                m.M21, m.M22, m.M23, 0,
                m.M31, m.M32, m.M33, 0,
                0, 0, 0, 1);
        }

        public override RenderOrderKey GetRenderOrderKey(Vector3 cameraPosition)
        {
            return new RenderOrderKey(ulong.MaxValue);
        }

        private static readonly VertexPosition[] s_vertices = new VertexPosition[]
        {
            // Top
            new VertexPosition(new Vector3(-20.0f,20.0f,-20.0f)),
            new VertexPosition(new Vector3(20.0f,20.0f,-20.0f)),
            new VertexPosition(new Vector3(20.0f,20.0f,20.0f)),
            new VertexPosition(new Vector3(-20.0f,20.0f,20.0f)),
            // Bottom
            new VertexPosition(new Vector3(-20.0f,-20.0f,20.0f)),
            new VertexPosition(new Vector3(20.0f,-20.0f,20.0f)),
            new VertexPosition(new Vector3(20.0f,-20.0f,-20.0f)),
            new VertexPosition(new Vector3(-20.0f,-20.0f,-20.0f)),
            // Left
            new VertexPosition(new Vector3(-20.0f,20.0f,-20.0f)),
            new VertexPosition(new Vector3(-20.0f,20.0f,20.0f)),
            new VertexPosition(new Vector3(-20.0f,-20.0f,20.0f)),
            new VertexPosition(new Vector3(-20.0f,-20.0f,-20.0f)),
            // Right
            new VertexPosition(new Vector3(20.0f,20.0f,20.0f)),
            new VertexPosition(new Vector3(20.0f,20.0f,-20.0f)),
            new VertexPosition(new Vector3(20.0f,-20.0f,-20.0f)),
            new VertexPosition(new Vector3(20.0f,-20.0f,20.0f)),
            // Back
            new VertexPosition(new Vector3(20.0f,20.0f,-20.0f)),
            new VertexPosition(new Vector3(-20.0f,20.0f,-20.0f)),
            new VertexPosition(new Vector3(-20.0f,-20.0f,-20.0f)),
            new VertexPosition(new Vector3(20.0f,-20.0f,-20.0f)),
            // Front
            new VertexPosition(new Vector3(-20.0f,20.0f,20.0f)),
            new VertexPosition(new Vector3(20.0f,20.0f,20.0f)),
            new VertexPosition(new Vector3(20.0f,-20.0f,20.0f)),
            new VertexPosition(new Vector3(-20.0f,-20.0f,20.0f)),
        };

        private static readonly ushort[] s_indices = new ushort[]
        {
            0,1,2, 0,2,3,
            4,5,6, 4,6,7,
            8,9,10, 8,10,11,
            12,13,14, 12,14,15,
            16,17,18, 16,18,19,
            20,21,22, 20,22,23,
        };
    }
}