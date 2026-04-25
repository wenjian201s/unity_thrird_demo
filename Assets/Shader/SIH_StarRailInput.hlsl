#ifndef SIH_STARRAIL_INPUT_INCLUDED // 作用：防止这个输入头文件被重复包含。原理：使用预处理宏守卫，避免重复声明纹理、采样器和常量缓冲。
#define SIH_STARRAIL_INPUT_INCLUDED // 作用：定义一个标记，表示该文件已经被包含。原理：下次再次 include 时，上面的 #ifndef 将不成立，从而跳过整段代码。

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl" // 作用：引入 URP 的核心基础库。原理：提供矩阵变换、坐标空间转换、基础数学工具、宏等通用功能。
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl" // 作用：引入 URP 光照库。原理：提供主光源获取、球谐光、光照结构体 Light 等接口。
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl" // 作用：引入深度纹理声明。原理：允许 Shader 访问场景深度纹理，例如边缘光会采样深度。
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl" // 作用：引入阴影相关库。原理：提供阴影坐标变换、主光阴影采样等功能。

// ------------------------------------- // 作用：分隔代码区域。原理：纯注释，不参与编译。
// Sampler Texture // 作用：说明下面开始声明纹理对象和采样器。原理：HLSL 里纹理资源和采样状态都要先声明后使用。
// BaseMap // 作用：说明下面是基础颜色贴图相关声明。原理：不同部位会使用不同的底色贴图。
TEXTURE2D(_BaseMap); // 作用：声明一个 2D 纹理 _BaseMap。原理：这对应材质面板里的主基础贴图资源。
SAMPLER(sampler_BaseMap); // 作用：声明 _BaseMap 对应的采样器。原理：采样器定义过滤方式、寻址方式等采样状态。
#if _AREA_FACE // 作用：如果当前编译变体是脸部区域。原理：由 shader keyword 控制，只编译脸部所需资源。
TEXTURE2D(_FaceColorMap); // 作用：声明脸部颜色贴图。原理：脸部区域使用独立底色图而不是通用底色。
SAMPLER(sampler_FaceColorMap); // 作用：声明脸部颜色贴图的采样器。原理：供 SAMPLE_TEXTURE2D 采样脸部贴图使用。
#elif _AREA_HAIR  // 作用：否则如果当前变体是头发区域。原理：不同区域贴图资源独立。
TEXTURE2D(_HairColorMap); // 作用：声明头发颜色贴图。原理：头发颜色通常单独控制。
SAMPLER(sampler_HairColorMap); // 作用：声明头发颜色贴图采样器。原理：为头发颜色图提供采样状态。
#elif _AREA_UPPERBODY // 作用：否则如果是上半身区域。原理：材质区域切换。
TEXTURE2D(_UpperBodyColorMap); // 作用：声明上半身颜色贴图。原理：衣服上半部分可能有独立底色图。
SAMPLER(sampler_UpperBodyColorMap); // 作用：声明上半身颜色贴图采样器。原理：供该区域底色采样使用。
#elif _AREA_LOWERBODY  // 作用：否则如果是下半身区域。原理：材质区域切换。
TEXTURE2D(_LowerBodyColorMap); // 作用：声明下半身颜色贴图。原理：下装/腿部可能使用独立底色图。
SAMPLER(sampler_LowerBodyColorMap); // 作用：声明下半身颜色贴图采样器。原理：供该区域底色采样使用。
#endif // 作用：结束基础颜色贴图区域分支。原理：每次只保留当前区域真正需要的纹理声明。

// LightMap // 作用：说明下面是 LightMap 声明。原理：头发和身体区域用 lightMap 存 AO、材质编号、高光 mask 等辅助数据。
#if _AREA_HAIR  // 作用：如果是头发区域。原理：头发使用头发专属 lightMap。
TEXTURE2D(_HairLightMap); // 作用：声明头发 lightMap。原理：其通道常用于阴影阈值、AO、高光 mask。
SAMPLER(sampler_HairLightMap); // 作用：声明头发 lightMap 的采样器。原理：供片元阶段采样头发辅助信息。
#elif _AREA_UPPERBODY // 作用：如果是上半身区域。原理：上半身有自己的 lightMap。
TEXTURE2D(_UpperBodyLightMap); // 作用：声明上半身 lightMap。原理：用于材质细分、高光、阴影等控制。
SAMPLER(sampler_UpperBodyLightMap); // 作用：声明上半身 lightMap 采样器。原理：用于 SAMPLE_TEXTURE2D 采样。
#elif _AREA_LOWERBODY  // 作用：如果是下半身区域。原理：下半身有自己的 lightMap。
TEXTURE2D(_LowerBodyLightMap); // 作用：声明下半身 lightMap。原理：提供下半身局部光照控制信息。
SAMPLER(sampler_LowerBodyLightMap); // 作用：声明下半身 lightMap 采样器。原理：供下半身区域使用。
#endif // 作用：结束 LightMap 条件编译。原理：脸部区域不会声明这些资源，因为脸部走 FaceMap 逻辑。

// RampColorMap // 作用：说明下面是 Ramp 渐变图声明。原理：toon 渲染会把明暗值映射到 ramp 纹理上得到分层色带。
#if _AREA_HAIR // 作用：如果当前区域是头发。原理：头发使用头发专属冷暖 ramp。
TEXTURE2D(_HairCoolRamp); // 作用：声明头发冷色 ramp 纹理。原理：冷光或冷阴影时从这里取色。
SAMPLER(sampler_HairCoolRamp); // 作用：声明头发冷色 ramp 采样器。原理：供采样冷色渐变。
TEXTURE2D(_HairWarmRamp); // 作用：声明头发暖色 ramp 纹理。原理：暖光环境时从这里取色。
SAMPLER(sampler_HairWarmRamp); // 作用：声明头发暖色 ramp 采样器。原理：供采样暖色渐变。
#elif _AREA_FACE || _AREA_UPPERBODY || _AREA_LOWERBODY // 作用：如果是脸或身体区域。原理：这些区域共用身体 ramp。
TEXTURE2D(_BodyCoolRamp); // 作用：声明身体冷色 ramp。原理：供脸、上半身、下半身阴影取冷色。
SAMPLER(sampler_BodyCoolRamp); // 作用：声明身体冷色 ramp 采样器。原理：供 SampleTexture 访问。
TEXTURE2D(_BodyWarmRamp); // 作用：声明身体暖色 ramp。原理：供脸、上半身、下半身阴影取暖色。
SAMPLER(sampler_BodyWarmRamp); // 作用：声明身体暖色 ramp 采样器。原理：供 SampleTexture 访问。
#endif // 作用：结束 Ramp 纹理条件编译。原理：不同区域只声明自己真正需要的 ramp。

// FaceShadow // 作用：说明下面是脸部阴影控制图声明。原理：脸部阴影不直接依赖普通 Lambert，而依赖专门的 face map。
#if _AREA_FACE // 作用：只有脸部区域需要。原理：FaceMap 主要服务于脸部 SDF 阴影、嘴部 AO、假描边等。
TEXTURE2D(_FaceMap); // 作用：声明脸部控制图。原理：其不同通道可能存脸影 SDF、AO mask、假描边 mask。
SAMPLER(sampler_FaceMap); // 作用：声明脸部控制图采样器。原理：供脸部片元逻辑使用。
#endif // 作用：结束 FaceMap 条件编译。原理：非脸部区域不会采样这个资源。

// Stockings // 作用：说明下面是丝袜/半透材质贴图声明。原理：身体某些区域需要额外的布料透感控制。
#if _AREA_UPPERBODY || _AREA_LOWERBODY // 作用：只有身体区域会用丝袜效果。原理：脸和头发不需要该资源。
TEXTURE2D(_UpperBodyStockings); // 作用：声明上半身丝袜/布料效果贴图。原理：供特殊服饰区域使用。
SAMPLER(sampler_UpperBodyStockings); // 作用：声明上半身丝袜贴图采样器。原理：用于采样 RG/B 控制通道。
TEXTURE2D(_LowerBodyStockings); // 作用：声明下半身丝袜贴图。原理：通常腿部丝袜效果主要依赖它。
SAMPLER(sampler_LowerBodyStockings); // 作用：声明下半身丝袜贴图采样器。原理：供丝袜渐变和细节扰动计算使用。
#endif // 作用：结束丝袜贴图条件编译。原理：非身体区域不声明这些资源。

// ------------------------------------- // 作用：分隔代码区域。原理：纯注释。
// CBUFFER // 作用：说明下面开始声明常量缓冲区。原理：材质参数会被打包到常量缓冲传给 GPU。
CBUFFER_START(UnityPerMaterial); // 作用：开始声明 UnityPerMaterial 常量缓冲。原理：Unity 会把每个材质实例的属性值填充到这个缓冲区中。
float _DebugColor; // 作用：声明调试模式参数。原理：在片元阶段可用来切换显示 baseColor、indirectLight、ramp 等中间结果。
float3 _HeadForward; // 作用：声明头部前方向。原理：脸部阴影会用它建立头部局部坐标系，判断光是从前还是后。
float3 _HeadRight; // 作用：声明头部右方向。原理：脸部阴影会用它判断光是从左还是右。

// BaseMap // 作用：说明下面是基础贴图变换参数。原理：Unity 会自动为主纹理提供 ST（Scale/Offset）参数。
float4 _BaseMap_ST; // 作用：声明基础贴图的 Tiling 和 Offset。原理：xy 一般是缩放，zw 一般是偏移，TRANSFORM_TEX 会用到它。

// FaceTintColor // 作用：说明下面是正反面染色参数。原理：双面材质会根据正反面乘不同颜色。
float3 _FrontFaceTintColor; // 作用：声明正面染色颜色。原理：角色正面朝向相机时乘这个色。
float3 _BackFaceTintColor; // 作用：声明背面染色颜色。原理：背面通常可略暗或偏色以增强层次。

// Alpha // 作用：说明下面是透明/裁剪参数。原理：用于透明度控制和 Alpha Clip。
float _Alpha; // 作用：声明整体透明度。原理：最终输出 alpha 会乘或直接取这个值。
float _AlphaClip; // 作用：声明 alpha 裁剪阈值。原理：clip(color.a - _AlphaClip) 时低于阈值的像素会被丢弃。

// Lighting // 作用：说明下面是光照相关参数。原理：用于控制间接光、主光、阴影软硬和 ramp 映射。
float _IndirectLightFlattenNormal; // 作用：声明间接光法线压平程度。原理：会把法线向 0 向量插值，减弱细碎法线对环境光的影响。
float _IndirectLightUsage; // 作用：声明间接光总权重。原理：控制 SH 环境光在最终颜色中的占比。
float _IndirectLightOcclusionUsage; // 作用：声明间接光 AO 使用强度。原理：控制 AO 对环境光压暗程度的影响。
float _IndirectLightMixBaseColor; // 作用：声明间接光混合底色程度。原理：让环境光带一点材质本色，避免显得发灰。

float _MainLightColorUsage; // 作用：声明主光颜色使用强度。原理：在去饱和光色和原始光色之间插值，控制灯光染色程度。
float _ShadowThresholdCenter; // 作用：声明阴影分界中心。原理：会整体推动 toon 阴影边界的位置。
float _ShadowThresholdSoftness; // 作用：声明阴影过渡软硬。原理：通常作为 smoothstep 的宽度，值越大边界越软。
float _ShadowRampOffset; // 作用：声明 ramp 采样偏移。原理：把阴影值挤压到 ramp 某一段区域，强化暗部或亮部细节。

// FaceShadow // 作用：说明下面是脸部阴影参数。原理：脸部使用单独的 SDF 阴影逻辑。
// #if _AREA_FACE // 作用：提示这些参数主要服务于脸部。原理：这里注释掉了条件编译，说明即使非脸部区域也统一放进同一个 CBUFFER，便于布局稳定。
float _FaceShadowOffset; // 作用：声明脸部阴影偏移。原理：对 faceMap 的 SDF 值做整体平移，微调脸影位置。
float _FaceShadowTransitionSoftness; // 作用：声明脸部阴影过渡柔和度。原理：作为 smoothstep 过渡宽度，控制脸影边缘软硬。
// #endif // 作用：注释说明结束。原理：不是实际编译指令。

// Specular // 作用：说明下面是高光参数。原理：用于 Blinn-Phong 高光和金属/非金属切换。
// #if _AREA_HAIR || _AREA_UPPERBODY || _AREA_LOWERBODY // 作用：提示这些参数主要用于头发和身体。原理：高光通常不直接用于脸部。
float _SpecularExpon; // 作用：声明高光指数。原理：指数越大，高光越集中越锐利。
float _SpecularKsNonMetal; // 作用：声明非金属高光反射率。原理：通常近似非金属 F0，常见值约为 0.04。
float _SpecularKsMetal; // 作用：声明金属高光反射率。原理：金属高光更强，且通常会染上底色。
float _SpecularMetalRange; // 作用：声明金属判定范围中心值。原理：lightMap 某通道接近这个值时会被视为金属材质区域。
float _SpecularBrightness; // 作用：声明整体高光亮度。原理：对高光结果统一乘一个增强系数。
// #endif // 作用：注释说明结束。原理：不是实际编译指令。

// Stockings // 作用：说明下面是丝袜/布料透感参数。原理：控制三段渐变颜色和视角过渡曲线。
// #if _AREA_UPPERBODY || _AREA_LOWERBODY // 作用：提示这些参数主要用于身体区域。原理：丝袜效果只在特定材质区域使用。
float3 _StockingsDarkColor; // 作用：声明丝袜暗部颜色。原理：视角不利区域、厚料区域更偏向这个颜色。
float3 _StockingsLightColor; // 作用：声明丝袜亮部颜色。原理：掠射或透感区域更偏向这个颜色。
float3 _StockingsTransitionColor; // 作用：声明丝袜中间过渡颜色。原理：用来在暗亮之间形成一条更自然的色带。
float _StockingsTransitionThreshold; // 作用：声明丝袜中间色带的位置。原理：决定渐变中段颜色在哪个 fac 值处出现。
float _StockingsTransitionPower; // 作用：声明丝袜过渡的幂次。原理：pow 会改变亮暗分布曲线的陡峭程度。
float _StockingsTransitionHardness; // 作用：声明丝袜过渡硬度。原理：进一步控制从暗到亮的边缘锐度。
float _StockingsTextureUsage; // 作用：声明丝袜细节纹理使用程度。原理：控制高频贴图细节对最终过渡的影响比重。
// #endif // 作用：注释说明结束。原理：不是实际编译指令。

// RimLight // 作用：说明下面是边缘光参数。原理：基于深度差或视角差检测模型边缘并加亮。
float _RimLightWidth; // 作用：声明边缘光宽度。原理：控制深度采样偏移量大小，从而决定 rim 的宽窄。
float _RimLightThreshold; // 作用：声明边缘光触发阈值。原理：只有偏移点深度和自身深度差超过这个阈值才更容易出现 rim。
float _RimLightFadeout; // 作用：声明边缘光衰减系数。原理：控制 rim 强度从边缘向内衰减的速度。
float3 _RimLightTintColor; // 作用：声明边缘光染色。原理：允许边缘光带上独立色相，而不是只跟随主光颜色。
float _RimLightBrightness; // 作用：声明边缘光亮度。原理：统一控制 rim 整体强度。
float _RimLightMixAlbedo; // 作用：声明边缘光混入底色程度。原理：让 rim 可以从纯光色变成带一些材质本色。

// Emission // 作用：说明下面是自发光参数。原理：让某些区域不依赖受光直接发亮。
// #if _EMISSION_ON // 作用：提示这些参数主要在 emission 开启时使用。原理：发光功能通常通过 keyword 控制。
float _EmissionMixBaseColor; // 作用：声明发光混合底色程度。原理：决定 emission 是纯色还是带底色。
float3 _EmissionTintColor; // 作用：声明自发光染色。原理：统一指定 emission 的色相。
float _EmissionIntensity; // 作用：声明自发光强度。原理：整体放大发光结果。
// #endif // 作用：注释说明结束。原理：不是实际编译指令。

// Outline // 作用：说明下面是描边参数。原理：描边 Pass 的顶点外扩和颜色曲线会用到这些值。
// #if _OUTLINE_ON // 作用：提示这些参数主要在描边启用时使用。原理：描边通过单独 Pass 和 keyword 控制。
float _OutlineWidth; // 作用：声明描边基础宽度。原理：顶点外扩时的初始距离，再叠加相机修正。
float _OutlineGamma; // 作用：声明描边颜色 gamma 曲线参数。原理：对 ramp 暗部颜色做 pow，强化或压暗描边墨线感。
// #endif // 作用：注释说明结束。原理：不是实际编译指令。

CBUFFER_END // 作用：结束 UnityPerMaterial 常量缓冲。原理：后续 Shader 代码可以直接读取这些材质参数。
#endif // 作用：结束头文件保护宏。原理：和文件开头的 #ifndef / #define 配对。