using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
namespace BitsPilani.Shared {
    public sealed class ChildViewSensoryRendererFeature : ScriptableRendererFeature {
        [Tooltip("Scene color effect shader; assigned by setup and retained in builds.")] public Shader effectShader;
        public RenderPassEvent passEvent=RenderPassEvent.BeforeRenderingPostProcessing;
        Material material;EffectPass pass;
        public override void Create(){CoreUtils.Destroy(material);material=effectShader?CoreUtils.CreateEngineMaterial(effectShader):null;pass=new EffectPass(material){renderPassEvent=passEvent};}
        public override void AddRenderPasses(ScriptableRenderer renderer,ref RenderingData data){
            if(!material||pass==null)return;var fx=ChildViewSensoryEffects.ForCamera(data.cameraData.camera);
            if(!fx||fx.CurrentPulse<=.0001f)return;renderer.EnqueuePass(pass);
        }
        protected override void Dispose(bool disposing){pass?.Dispose();CoreUtils.Destroy(material);}
        sealed class EffectPass:ScriptableRenderPass {
            readonly Material mat;RTHandle temporary;
            public EffectPass(Material material){mat=material;requiresIntermediateTexture=true;ConfigureInput(ScriptableRenderPassInput.Color);}
            static void Set(Material m,ChildViewSensoryEffects fx){m.SetVector("_Pulse",fx.CurrentShaderPulse);m.SetColor("_FlashColor",fx.flashColor);}
            public override void RecordRenderGraph(RenderGraph graph,ContextContainer frame){
                var camera=frame.Get<UniversalCameraData>().camera;var fx=ChildViewSensoryEffects.ForCamera(camera);if(!fx||!mat)return;
                var resources=frame.Get<UniversalResourceData>();if(resources.isActiveTargetBackBuffer)return;
                var source=resources.activeColorTexture;var desc=graph.GetTextureDesc(source);desc.name="Child view brightness and blur";desc.clearBuffer=false;desc.msaaSamples=MSAASamples.None;
                var destination=graph.CreateTexture(desc);var properties=new MaterialPropertyBlock();
                properties.SetVector("_Pulse",fx.CurrentShaderPulse);properties.SetColor("_FlashColor",fx.flashColor);
                properties.SetVector("_ChildViewTexelSize",new Vector4(1f/Mathf.Max(1,desc.width),1f/Mathf.Max(1,desc.height),desc.width,desc.height));
                var parameters=new RenderGraphUtils.BlitMaterialParameters(source,destination,mat,0,properties);
                graph.AddBlitPass(parameters,passName:"Child view editable brightness/blur");resources.cameraColor=destination;
            }
#pragma warning disable 618,672
            public override void Configure(CommandBuffer cmd,RenderTextureDescriptor descriptor){descriptor.depthBufferBits=0;descriptor.msaaSamples=1;RenderingUtils.ReAllocateHandleIfNeeded(ref temporary,descriptor,FilterMode.Bilinear,TextureWrapMode.Clamp,name:"ChildViewPulseTemp");}
            public override void Execute(ScriptableRenderContext context,ref RenderingData data){
                var fx=ChildViewSensoryEffects.ForCamera(data.cameraData.camera);if(!fx||!mat||temporary==null)return;Set(mat,fx);var descriptor=data.cameraData.cameraTargetDescriptor;mat.SetVector("_ChildViewTexelSize",new Vector4(1f/Mathf.Max(1,descriptor.width),1f/Mathf.Max(1,descriptor.height),descriptor.width,descriptor.height));
                var source=data.cameraData.renderer.cameraColorTargetHandle;var cmd=CommandBufferPool.Get("Child view editable brightness/blur");
                Blitter.BlitCameraTexture(cmd,source,temporary,mat,0);Blitter.BlitCameraTexture(cmd,temporary,source);context.ExecuteCommandBuffer(cmd);CommandBufferPool.Release(cmd);
            }
#pragma warning restore 618,672
            public void Dispose(){temporary?.Release();temporary=null;}
        }
    }
}
