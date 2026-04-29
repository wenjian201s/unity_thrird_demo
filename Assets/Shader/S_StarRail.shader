Shader "URP/Character/S_StarRail" // 定义一个 Shader 资源，路径会显示在 Unity 材质/Shader 菜单中；本质上是给这套 URP 角色渲染器注册名字。
{ // Shader 定义体开始；后续包含材质属性、SubShader、Fallback 等完整配置。
    Properties // 声明材质面板参数；Unity 会把这里的字段暴露给 Inspector，并自动生成对应常量/纹理绑定。
    { // Properties 块开始；其中每一项既是编辑器 UI 声明，也是运行时材质参数入口。
        [Enum(None,1,baseColor,2,indirectLightColor,3,mianLightColor,4,mainLightShadow,5,rampColor,6,specularColor,7)] _DebugColor ("Debug Color", Int) = 1 // 枚举调试输出模式；通常让片元阶段输出某个中间结果，便于排查颜色、阴影、ramp、高光等通道。
        [KeywordEnum (None, Face, Hair, UpperBody, LowerBody)] _Area("Material area", float) = 0 // 定义材质所属区域并生成关键字变体；原理是通过 KeywordEnum 生成 _AREA_FACE/_AREA_HAIR 等编译宏，针对头发/脸/上下身走不同分支。
        [HideInInspector] _HeadForward("", Vector) = (0,0,1) // 隐藏在 Inspector 中的头部前方向；常用于脸部朝向阴影计算，避免直接依赖模型局部坐标不稳定。
        [HideInInspector] _HeadRight("", Vector) = (1,0,0) // 隐藏的头部右方向；通常与 _HeadForward 组合成头部局部平面基底，用于判断光在脸上的左右分布。

        [Header (Base Color)] // Inspector 分组标题：基础颜色相关参数；只影响编辑器显示，不影响运行逻辑。
        _BaseMap ("", 2D) = "white" {} // 基础贴图；通常作为默认主纹理采样源，白图意味着不额外改变颜色。
        [NoScaleOffset] _FaceColorMap ("Face color map (Default white)", 2D) = "white" {} // 脸部颜色贴图，禁止平铺和偏移；因为角色 UV 一般固定，不希望材质实例改 Tiling/Offset 破坏分区。
        [NoScaleOffset] _HairColorMap ("Hair color map (Default white)", 2D) = "white" {} // 头发颜色贴图；按区域单独采样颜色，支持不同身体部分共用一套 Shader 逻辑。
        [NoScaleOffset] _UpperBodyColorMap ("Upper body color map (Default white)", 2D) = "white" {} // 上半身颜色贴图；通过区域关键字选择这个纹理。
        [NoScaleOffset] _LowerBodyColorMap ("Lower body color map (Default white)", 2D) = "white" {} // 下半身颜色贴图；同理用于下装/腿部等区域独立着色。
        _FrontFaceTintColor("Front face tint color (Default white)",Color) = (1,1,1) // 正面颜色染色；常用于正反面区分或正面额外提亮，本质是乘色调制。
        _BackFaceTintColor("Back face tint color (Default white)",Color) = (1,1,1) // 背面颜色染色；通常用于双面材质时让背面略暗/偏色，形成纸片或发片层次。
        [Toggle(_UseAlphaClipping)]_UseAlphaClipping("Use alpha clipping (Default NO)", Float) = 0 // 开启 Alpha Clip 的开关；会生成宏，片元里通常用 clip(alpha-threshold) 丢弃像素。
        _Alpha("Alpha (Default 1)", Range(0,1)) = 1 // 整体透明度因子；一般与贴图 alpha 相乘后参与裁剪或混合。
        _AlphaClip("Alpha clip (Default 0.333)", Range(0,1)) = 0.333 // Alpha 裁剪阈值；低于此值的像素会被 clip 丢弃，常用于发丝/半透明边缘硬裁切。

        [Header(Light Map)] // Inspector 分组标题：光照辅助贴图区域。
        [NoScaleOffset] _HairLightMap("Hair light map (Default black)",2D) = "black" {} // 头发 light map；常把 AO、金属度、材质 mask、高光区域等打包到通道里。
        [NoScaleOffset] _UpperBodyLightMap("Upper body map (Default black)",2D) = "black" {} // 上半身 light map；与颜色图分离能让光照遮罩不受底色变化影响。
        [NoScaleOffset] _LowerBodyLightMap("Lower body map (Default black)",2D) = "black" {} // 下半身 light map；通常同样承载阴影阈值偏移/高光mask/丝袜mask等通道数据。

        [Header(Ramp Map)] // Inspector 分组标题：ramp 渐变贴图，用于 toon 分层光照。
        [NoScaleOffset] _HairCoolRamp("Hair cool ramp (Default white)",2D) = "white" {} // 头发冷色 ramp；当环境/阴影偏冷时用这条渐变来重映射明暗。
        [NoScaleOffset] _HairWarmRamp("Hair warm ramp (Default white)",2D) = "white" {} // 头发暖色 ramp；主光偏暖时用于卡通分层着色。
        [NoScaleOffset] _BodyCoolRamp("Body cool ramp (Default white)",2D) = "white" {} // 身体冷色 ramp；控制躯干等区域 toon 阴影过渡。
        [NoScaleOffset] _BodyWarmRamp("Body warm ramp (Default white)",2D) = "white" {} // 身体暖色 ramp；与冷 ramp 配对，实现暖光/冷影或不同光环境切换。

        [Header(Indirect Lighting)] // Inspector 分组标题：间接光参数。
        _IndirectLightFlattenNormal("Indirect light flatten normal (Default 0)",Range(0,1)) = 0 // 间接光法线压平程度；原理是把法线朝某个平均方向插值，降低法线细节对环境光的影响，让脸/身体更“二次元平涂”。
        _IndirectLightUsage("Indirect light usage (Default 0.5)",Range(0,1)) = 0.5 // 间接光总体权重；控制环境光/球谐光在最终颜色中的占比。
        _IndirectLightOcclusionUsage("Indirect light occlusion usage (Default 0.5)",Range(0,1)) = 0.5 // 间接光 AO 使用权重；通常把 light map 某通道作为 AO，插值决定环境光被遮蔽多少。
        _IndirectLightMixBaseColor("Indirect light mix base color (Default 1)",Range(0,1)) = 1 // 间接光与底色混合强度；常见做法是让环境光颜色向 albedo 偏移，避免纯灰环境光把角色洗脏。

        [Header(Main Lighting)] // Inspector 分组标题：主光（方向光）参数。
        _MainLightBrightnessFactor("Main light brightness factor (Default 1)",Range(0,1)) = 1 // 主光亮度因子；用于整体压缩或增强方向光贡献。
        _MainLightColorUsage("Main light color usage (Default 1)",Range(0,1)) = 1 // 主光颜色使用程度；0 时更接近白光明暗，1 时完全使用灯光颜色。
        _ShadowThresholdCenter("Shadow threshold center (Default 0)",Range(-1,1)) = 0 // 阴影阈值中心；通常加在 N·L 或某种自定义明暗值上，控制明暗分界线前后移动。
        _ShadowThresholdSoftness("Shadow threshold softness (Default 0.1)",Range(0,1)) = 0.1 // 阴影软硬程度；通常用于 smoothstep 的过渡宽度，让 toon 分层边缘更软或更硬。
        _ShadowRampOffset("Shadow ramp offset (Default 0.75)",Range(0,1)) = 0.75 // 阴影 ramp 采样偏移；本质是改变明暗值映射到 ramp 的位置，调整阴影层级比例。
        _ShadowBoost("Shadow Boost (Default 1)", Range(0.0, 1.0)) = 1.0 // 阴影增强系数；一般用来提高暗部存在感或加重 toon 层次。

        [Header(Face)] // Inspector 分组标题：脸部专用阴影参数。
        [NoScaleOffset] _FaceMap("Face map (Default black)",2D) = "black" {} // 脸部专用贴图；星铁/原神类 Shader 常用此图存脸部阴影遮罩、SDF 或左右阴影控制信息。
        _FaceShadowOffset("Face shadow offset (Default -0.01)",Range(-1,1)) = -0.01 // 脸部阴影偏移；控制脸部阴影分界线位置，防止阴影切到五官不自然。
        _FaceShadowTransitionSoftness("Face shadow transition softness (Default 0.05)", Range(0,1)) = 0.05 // 脸部阴影过渡柔和度；通常用于脸部阴影 smoothstep，做出更稳定更“画风化”的边缘。

        [Header(Specular)] // Inspector 分组标题：高光参数。
        _SpecularExpon("Specular exponent (Default 50)",Range(0,100)) = 50 // 高光指数；类似 Blinn-Phong/Phong 中的指数，值越大高光越集中越尖锐。
        _SpecularKsNonMetal("Specular KS non-metal (Default 0.04)",Range(0,1)) = 0.04 // 非金属镜面反射率；0.04 是常见 dielectric F0 近似值。
        _SpecularKsMetal("Specular KS metal (Default 1)",Range(0,1)) = 1 // 金属镜面反射率；金属通常有更高且带色的镜面反射，这里给最大强度上限。
        _SpecularMetalRange("Specular Metal Range (Default 0.52)",Range(0,1)) = 0.52 // 金属范围/阈值；通常把 light map 某通道与此值比较，决定像素更像金属还是非金属。
        _SpecularBrightness("Specular brightness (Default 1)",Range(0,10)) = 10 // 高光亮度倍增；在计算镜面项后再统一放大。

        [Header(Stockings)] // Inspector 分组标题：丝袜/半透布料效果参数。
        [NoScaleOffset] _UpperBodyStockings("Upper body stockings (Default black)",2D) = "black" {} // 上半身丝袜效果贴图；某些服装部件也可能用这个做半透布料。
        [NoScaleOffset] _LowerBodyStockings("Lower body stockings (Default black)",2D) = "black" {} // 下半身丝袜贴图；常用于腿部深浅变化、边缘透肤感等效果。
        _StockingsDarkColor("Stockings dark color (Default black)",Color) = (0,0,0) // 丝袜暗部颜色；决定覆盖后的最深色调。
        [HDR] _StockingsLightColor("Stockings light color (Default 1.8, 1.48299, 0.856821)",Color) = (1.8, 1.48299, 0.856821) // 丝袜亮部颜色，允许 HDR；用于模拟布料反光/透肤高亮区域。
        [HDR] _StockingsTransitionColor("Stockings transition color (Default 0.360381, 0.242986, 0.358131)",Color) = (0.360381, 0.242986, 0.358131) // 丝袜过渡颜色；用于暗亮之间的色带，避免直接线性插值太生硬。
        _StockingsTransitionThreshold("Stockings transition Threshold (Default 0.58)",Range(0,1)) = 0.58 // 丝袜过渡阈值；决定深浅分界位置。
        _StockingsTransitionPower("Stockings transition power (Default 1)",Range(0,50)) = 1 // 丝袜过渡幂次；通过 pow 改变过渡曲线形状。
        _StockingsTransitionHardness("Stockings transition hardness (Default 0.4)",Range(0,1)) = 0.4 // 丝袜过渡硬度；本质仍是控制边缘锐度。
        _StockingsTextureUsage("Stockings texture usage (Default 0.1)",Range(0,1)) = 0.1 // 贴图使用程度；控制丝袜贴图细节对最终颜色的影响权重。

        [Header(Rim Lighting)] // Inspector 分组标题：边缘光参数。
        _RimLightWidth("Rim light width (Default 1)", Range(0, 10)) = 1 // 边缘光宽度；通常基于视线方向与法线点乘结果控制，数值越大边缘带越宽。
        _RimLightThreshold("Rim light threshold (Default 0.05)", Range(-1, 1)) = 0.05 // 边缘光触发阈值；决定从法线-视线关系的哪个区间开始出现 rim。
        _RimLightFadeout("Rim light fadeout (Default 1)", Range(0, 1)) = 1 // 边缘光衰减；控制其从边缘向内的减弱程度。
        [HDR] _RimLightTintColor("Rim light tint color (Default white)",Color) = (1,1,1) // 边缘光颜色，允许 HDR；便于做强烈轮廓泛光。
        _RimLightBrightness("Rim light brightness (Default 1)", Range(0, 1)) = 1 // 边缘光亮度系数；乘到 rim 最终结果上。
        _RimLightMixAlbedo("Rim light mix albedo (Default 0.9)",Range(0, 1)) = 0.9 // 边缘光混入底色程度；让 rim 不是纯加法白边，而保留材质本身色相。

        [Header(Emission)] // Inspector 分组标题：自发光参数。
        [Toggle(_EMISSION_ON)] _UseEmission("Use emission (Default NO)",float) = 0 // 自发光开关；启用后生成 _EMISSION_ON 宏，片元阶段可能额外输出 emissive。
        _EmissionMixBaseColor("Emission mix base color (Default 1)", Range(0,1)) = 1 // 自发光混合底色比例；常见用于让发光继承 albedo，而非完全纯色。
        _EmissionTintColor("Emission tint color (Default white)", Color) = (1,1,1)  // 自发光染色；给 emissive 最终着色。
        _EmissionIntensity("Emission intensity (Default 1)", Range(0,100)) = 1 // 自发光强度；通常直接乘在 emission 上。

        [Header(Outline)] // Inspector 分组标题：描边参数。
        [Toggle(_OUTLINE_ON)] _UseOutline("Use outline (Default YES)", float ) = 1 // 描边开关；决定是否编译/执行描边 pass 的有效逻辑。
        [Toggle(_OUTLINE_VERTEX_COLOR_SMOOTH_NORMAL)] _OutlineUseVertexColorSmoothNormal("Use vertex color smooth normal (Default NO)", float) = 0 // 是否使用顶点色中存储的平滑法线；常见于卡通描边，通过预烘焙法线解决硬边破裂。
        _OutlineWidth("Outline width (Default 1)", Range(0,10)) = 1 // 描边宽度；通常在顶点阶段沿法线/平滑法线外扩。
        _OutlineGamma("Outline gamma (Default 16)", Range(1,255)) = 16 // 描边 gamma/曲线参数；可能用于屏幕空间宽度补偿或远近变化曲线控制。

        [Header(Surface Options)] // Inspector 分组标题：通用表面渲染状态。
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull Mode (Default Back)", Float) = 2 // 面剔除模式；2 通常是 Back，默认剔除背面以减少 overdraw。
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlendMode ("SrcBlendMode (Default One)", Float) = 1 // 源混合因子；用于颜色混合公式 Src*SrcFactor + Dst*DstFactor。
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlendMode ("DstBlendMode (Default Zero)", Float) = 0 // 目标混合因子；与源因子共同定义最终混合。
        [Enum(UnityEngine.Rendering.BlendOp)] _BlendOp ("BlendOp (Default Add)", Float) = 0 // 混合操作类型；默认 Add，即源目标加和。
        [Enum(Off,0, On,1)] _ZWrite("ZWrite (Default On)",Float) = 1 // 是否写入深度；不透明物体通常开启以保证遮挡关系正确。
        _StencilRef ("Stencil reference (Default 0)",Range(0,255)) = 0 // 模板测试参考值；写模板或比模板时作为基准值。
        [Enum(UnityEngine.Rendering.CompareFunction)] _StencilComp("Stencil comparison (Default disabled)",Int) = 0 // 模板比较函数；决定当前像素与 stencil buffer 如何比较。
        [Enum(UnityEngine.Rendering.StencilOp)] _StencilPassOp("Stencil pass comparison (Default keep)",Int) = 0 // 模板测试通过且深度通过时的 stencil 操作；默认 keep。
        [Enum(UnityEngine.Rendering.StencilOp)] _StencilFailOp("Stencil fail comparison (Default keep)",Int) = 0 // 模板测试失败时的 stencil 操作；可用于遮罩控制。
        [Enum(UnityEngine.Rendering.StencilOp)] _StencilZFailOp("Stencil z fail comparison (Default keep)",Int) = 0 // 模板通过但深度失败时的 stencil 操作；用于更复杂的遮罩/轮廓技巧。

        [Header(Draw Overlay)] // Inspector 分组标题：叠加绘制 pass 参数。
        [Toggle(_DRAW_OVERLAY_ON)] _UseDrawOverlay("Use draw overlay (Default NO)",float) = 0 // Overlay pass 开关；打开后 DrawOverlay pass 会真正参与渲染。
        [Enum(UnityEngine.Rendering.BlendMode)] _ScrBlendModeOverlay("Overlay pass scr blend mode (Default One)",Float) = 1 // Overlay pass 的源混合因子；控制叠加层如何写到目标。
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlendModeOverlay("Overlay pass dst blend mode (Default Zero)", Float) = 0 // Overlay pass 的目标混合因子；与上面共同组成混合公式。
        [Enum(UnityEngine.Rendering.BlendOp)] _BlendOpOverlay("Overlay pass blend operation (Default Add)", Float) = 0 // Overlay pass 混合操作；默认 Add。
        _StencilRefOverlay ("Overlay pass stencil reference (Default 0)", Range(0,255)) = 0 // Overlay pass 模板参考值；可让叠加层只绘制在指定区域。
        [Enum(UnityEngine.Rendering.CompareFunction)] _StencilCompOverlay("Overlay pass stencil comparison (Default disabled)",Int) = 0 // Overlay pass 模板比较函数；用来决定叠加层是否通过 stencil。
 
    } // Properties 块结束；以上参数会成为材质 UI 和 GPU 常量/纹理绑定入口。
    SubShader // 开始定义一个 SubShader；Unity 会根据平台和能力选择可用的 SubShader。
    { // SubShader 主体开始。
        LOD 100 // 指定该 SubShader 的复杂度级别；Unity 可根据全局 LOD 设置选择更低复杂度版本。

        HLSLINCLUDE // HLSL 公共包含区开始；这里写的内容会被后续各个 Pass 共用。
        // ------------------------------------- // 分隔注释，无运行作用，仅提升可读性。
        // Material Keywords // 说明下面是材质关键字编译选项。
        #pragma shader_feature_local _AREA_FACE // 为脸部区域生成本地关键字变体；只在当前 Shader 内部生效，减少全局关键字污染。
        #pragma shader_feature_local _AREA_HAIR // 为头发区域生成本地关键字变体；编译时可裁掉无关分支。
        #pragma shader_feature_local _AREA_UPPERBODY // 为上半身区域生成本地关键字变体。
        #pragma shader_feature_local _AREA_LOWERBODY // 为下半身区域生成本地关键字变体。
        #pragma shader_feature_local _OUTLINE_ON // 描边开关编译宏；开启时相关 pass/include 逻辑生效。
        #pragma shader_feature_local _OUTLINE_VERTEX_COLOR_SMOOTH_NORMAL // 描边使用顶点色平滑法线的变体开关。
        #pragma shader_feature_local _DRAW_OVERLAY_ON // Overlay pass 开关的本地关键字。
        #pragma shader_feature_local _EMISSION_ON // 发光开关的本地关键字。
        #pragma shader_feature_local_fragment _UseAlphaClipping // 仅在片元阶段启用 alpha clip 变体；避免无谓增加顶点阶段变体数。
        ENDHLSL // 公共 HLSL 包含区结束。

        Pass // 开始定义一个 Pass；每个 Pass 对应一次独立绘制流程。
        { // ShadowCaster pass 块开始。
            Name "ShadowCaster" // Pass 名称为 ShadowCaster；供 Unity 在阴影贴图生成阶段调用。
            Tags // 设置此 pass 的标签。
            { // Tags 开始。
                "LightMode" = "ShadowCaster" // 标记这是阴影投射 pass；URP 会把它用于主光/点光阴影图渲染。
            } // ShadowCaster 的 Tags 结束。

            // ------------------------------------- // 分隔注释。
            // Render State Commands // 说明下面是渲染状态命令。
            ZWrite [_ZWrite] // 是否写深度由材质参数控制；阴影 pass 通常需要写深度到阴影贴图。
            ZTest LEqual // 深度测试使用 LessEqual；当前像素深度小于等于已有深度时通过。
            ColorMask 0 // 不写颜色缓冲；阴影 pass 只关心深度，不需要颜色输出。
            Cull [_Cull] // 使用材质设置的剔除模式；影响阴影体积的正反面参与情况。

            HLSLPROGRAM // 进入该 Pass 的 HLSL 程序体。
            #pragma exclude_renderers gles gles3 glcore // 排除部分渲染后端；通常是因为目标代码要求更高特性或作者没适配这些平台。
            #pragma target 4.5 // 指定 Shader Model 4.5；允许使用更现代的 GPU 功能和语法。

            // ------------------------------------- // 分隔注释。
            // Material Keywords // 说明下面是此 pass 需要的材质变体。
            #pragma shader_feature_local _ALPHATEST_ON // alpha test 变体；阴影投射时发丝等裁剪材质要正确裁出阴影轮廓。
            #pragma shader_feature_local_fragment _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A // 平滑度来自 Albedo Alpha 的变体；来自 URP 标准 Lit 兼容输入。

            //-------------------------------------- // 分隔注释。
            // GPU Instancing // 说明下面启用 GPU 实例化支持。
            #pragma multi_compile_instancing // 编译实例化变体；允许多个相同网格材质对象批量绘制。
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl" // 引入 DOTS/实例化相关辅助定义，并带上其中 pragma。

            // ------------------------------------- // 分隔注释。
            // Universal Pipeline keywords // 说明这里通常放 URP 管线相关关键字；本 pass 这里未额外声明。

            // ------------------------------------- // 分隔注释。
            // Unity defined keywords // 说明下面是 Unity 内置关键字。
            #pragma multi_compile_fragment _ LOD_FADE_CROSSFADE // 编译 LOD 淡入淡出变体；支持模型 LOD 切换时的 crossfade。

            // This is used during shadow map generation to differentiate between directional and punctual light shadows, as they use different formulas to apply Normal Bias // 注释说明：方向光和点/聚光在阴影 normal bias 上公式不同，需要区分。
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW // 顶点阶段编译“投射点光/聚光阴影”的变体，修正阴影偏移计算。
            
            // ------------------------------------- // 分隔注释。
            // Shader Stages // 说明下面指定该 Pass 的顶点/片元入口函数。
            #pragma vertex ShadowPassVertex // 指定顶点着色器入口函数；来自后面 include 的 URP 阴影 pass 实现。
            #pragma fragment ShadowPassFragment // 指定片元着色器入口函数；负责阴影深度写入/裁剪。

            // ------------------------------------- // 分隔注释。
            // Includes // 说明下面引入依赖代码。
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl" // 引入 URP Lit 输入定义，包含材质纹理/常量缓冲兼容接口。
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl" // 引入 URP 标准 ShadowCaster 实现，复用其阴影投射逻辑。
            ENDHLSL // ShadowCaster pass 的 HLSL 程序结束。
        } // ShadowCaster pass 结束。

        Pass // 开始定义第二个 Pass。
        { // DepthOnly pass 块开始。
            Name "DepthOnly" // Pass 名称为 DepthOnly；通常用于只写场景深度。
            Tags // 标签开始。
            { // Tags 块开始。
                "LightMode" = "DepthOnly" // 告诉 URP 这是深度预通道/深度纹理生成 pass。
            } // DepthOnly 标签结束。

            // ------------------------------------- // 分隔注释。
            // Render State Commands // 渲染状态说明。
            ZWrite [_ZWrite] // 是否写深度受材质参数控制；DepthOnly pass 的核心就是写深度。
            ColorMask R // 只写颜色缓冲的 R 通道；某些平台/实现需要至少写一个通道以保持 pass 有效，但主要目标还是深度。
            Cull[_Cull] // 使用材质剔除模式，保证与主 pass 一致的几何可见性。

            HLSLPROGRAM // 进入 DepthOnly pass 程序体。
            #pragma exclude_renderers gles gles3 glcore // 排除不支持/未适配的图形后端。
            #pragma target 4.5 // 目标 Shader Model 4.5。

            // ------------------------------------- // 分隔注释。
            // Material Keywords // 材质变体说明。
            #pragma shader_feature_local _ALPHATEST_ON // 深度 pass 也要支持 alpha test，否则头发深度轮廓会错误。
            #pragma shader_feature_local_fragment _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A // 与 URP Lit 输入兼容的平滑度通道变体。

            // ------------------------------------- // 分隔注释。
            // Unity defined keywords // Unity 内置关键字。
            #pragma multi_compile_fragment _ LOD_FADE_CROSSFADE // 支持 LOD crossfade 时的深度写入一致性。

            //-------------------------------------- // 分隔注释。
            // GPU Instancing // 实例化说明。
            #pragma multi_compile_instancing // 开启 GPU Instancing 编译。
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl" // 引入 DOTS/实例化支持。

            // ------------------------------------- // 分隔注释。
            // Shader Stages // 指定着色器阶段入口。
            #pragma vertex DepthOnlyVertex // 顶点入口，来自 URP 标准深度 pass。
            #pragma fragment DepthOnlyFragment // 片元入口，负责深度输出/裁剪。

            // ------------------------------------- // 分隔注释。
            // Includes // 引入实现。
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl" // 引入 Lit 输入定义。
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl" // 直接复用 URP 的 DepthOnly pass 实现。
            ENDHLSL // DepthOnly pass 程序结束。
        } // DepthOnly pass 结束。

        Pass // 开始第三个 Pass。
        { // DepthNormals pass 块开始。
            Name "DepthNormals" // Pass 名称为 DepthNormals；用于输出深度+法线纹理。
            Tags // 标签块开始。
            { // 标签体开始。
                "LightMode" = "DepthNormals" // 告诉管线这个 pass 用于生成深度法线纹理，常供 SSAO、屏幕后处理使用。
            } // 标签结束。

            // ------------------------------------- // 分隔注释。
            // Render State Commands // 渲染状态说明。
            ZWrite [_ZWrite] // 控制是否写入深度。
            Cull[_Cull] // 控制面剔除方式。

            HLSLPROGRAM // 进入 DepthNormals pass 程序体。
            #pragma exclude_renderers gles gles3 glcore // 排除部分后端。
            #pragma target 4.5 // 目标 Shader Model 4.5。

            // ------------------------------------- // 分隔注释。
            // Material Keywords // 材质关键字说明。
            #pragma shader_feature_local _NORMALMAP // 支持法线贴图变体；输出更准确的屏幕法线。
            #pragma shader_feature_local _PARALLAXMAP // 支持视差贴图变体；与 Lit 兼容，但这里未必实际使用。
            #pragma shader_feature_local _ _DETAIL_MULX2 _DETAIL_SCALED // 细节贴图模式变体；沿用 URP Lit 标准定义。
            #pragma shader_feature_local_fragment _ALPHATEST_ON // 片元 alpha test 变体；保证发片等法线/深度轮廓正确。
            #pragma shader_feature_local_fragment _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A // 平滑度通道变体；仍是 Lit 输入兼容项。

            // ------------------------------------- // 分隔注释。
            // Unity defined keywords // Unity 内置关键字说明。
            #pragma multi_compile_fragment _ LOD_FADE_CROSSFADE // 支持 LOD 交叉淡出变体。

            // ------------------------------------- // 分隔注释。
            // Universal Pipeline keywords // URP 相关关键字说明。
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl" // 引入渲染层支持，供法线深度 pass 正确处理 URP rendering layers。

            //-------------------------------------- // 分隔注释。
            // GPU Instancing // 实例化说明。
            #pragma multi_compile_instancing // 开启实例化编译。
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl" // 引入 DOTS 支持。

            // ------------------------------------- // 分隔注释。
            // Shader Stages // 着色器阶段说明。
            #pragma vertex DepthNormalsVertex // 指定顶点入口。
            #pragma fragment DepthNormalsFragment // 指定片元入口，输出法线编码结果。

            // ------------------------------------- // 分隔注释。
            // Includes // 依赖引入说明。
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl" // 引入 Lit 输入结构。
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitDepthNormalsPass.hlsl" // 复用 URP 官方的深度法线 pass 实现。
            ENDHLSL // DepthNormals pass 程序结束。
        } // DepthNormals pass 结束。

        Pass // 开始第四个 Pass。
        { // DrawCore pass 块开始；这是角色主体真正绘制的核心 pass。
            Name "DrawCore" // Pass 名称 DrawCore；自定义命名，用于主体着色。
            Tags // 标签块开始。
            { // 标签体开始。
                "RenderPipeline" = "UniversalPipeline" // 指明此 pass 仅用于 URP。
                "RenderType" = "Opaque" // 声明渲染类型为不透明；影响排序、替换 shader、某些管线行为。
            } // DrawCore 标签结束。

            // ------------------------------------- // 分隔注释。
            // Render State Commands // 渲染状态说明。
            Cull [_Cull]// 根据材质设置剔除正/背面。
            Stencil // 开始模板测试配置；常用于角色遮罩、描边隔离、后效限制区域。
            { // Stencil 状态块开始。
                Ref [_StencilRef] // 模板参考值。
                Comp [_StencilComp] // 模板比较函数；决定当前像素是否通过 stencil。
                Pass [_StencilPassOp] // 模板测试通过时对 stencil buffer 的操作。
                Fail [_StencilFailOp] // 模板测试失败时对 stencil buffer 的操作。
                ZFail [_StencilZFailOp] // 模板通过但深度失败时对 stencil 的操作。
            } // Stencil 配置结束。
            Blend [_SrcBlendMode] [_DstBlendMode] // 设置颜色混合因子；虽然 RenderType 是 Opaque，但也允许通过材质切成半透明式混合。
            BlendOp [_BlendOp] // 设置颜色混合操作，例如 Add/Subtract。
            ZWrite [_ZWrite] // 控制主体 pass 是否写深度。

            HLSLPROGRAM // 进入 DrawCore 的 HLSL 程序体。
            // ------------------------------------- // 分隔注释。
            // Universal Pipeline keywords // URP 灯光/阴影关键字说明。
            #pragma multi_compile _MAIN_LIGHT_SHADOWS // 编译主光阴影开关变体。
            #pragma multi_compile _MAIN_LIGHT_SHADOWS_CASCADE // 编译主光级联阴影变体。
            #pragma multi_compile_fragment _SHADOWS_SOFT // 编译软阴影片元变体。

            // ------------------------------------- // 分隔注释。
            // Unity defined keywords // Unity 内置关键字说明。
            #pragma multi_compile_fog // 编译雾效变体，使角色能正确参与场景雾。

            // ------------------------------------- // 分隔注释。
            // Shader Stages // 指定自定义顶点/片元入口。
            #pragma vertex StarRailPassVertex // 顶点入口函数；应在下面 include 的自定义 HLSL 中实现。
            #pragma fragment StarRailPassFragment // 片元入口函数；主体 toon/脸阴影/高光/rim 等大概率都在此实现。

            #include "./SIH_StarRailInput.hlsl" // 引入角色 Shader 的输入定义，如 CBUFFER、纹理采样函数、公共结构体等。
            #include "./SIH_StarRailDrawCorePass.hlsl" // 引入主体绘制核心逻辑；实际星铁风角色渲染大概率主要写在这里。
            ENDHLSL // DrawCore pass 程序结束。
        } // DrawCore pass 结束。

        Pass // 开始第五个 Pass。
        { // DrawOverlay pass 块开始；用于额外叠加绘制。
            Name "DrawOverlay" // Pass 名称为 DrawOverlay。
            Tags // 标签块开始。
            { // 标签体开始。
                "RenderPipeline" = "UniversalPipeline" // 仅在 URP 中使用。
                "RenderType" = "Opaque" // 标记为不透明类型。
                "LightMode" = "UniversalForward" // 指定此 pass 走 URP 正向渲染主流程。
            } // 标签结束。

            // ------------------------------------- // 分隔注释。
            // Render State Commands // 渲染状态说明。
            Cull[_Cull] // 使用材质设定的剔除模式。
            Stencil // Overlay pass 的模板设置开始。
            { // stencil 块开始。
                Ref [_StencilRefOverlay] // Overlay 使用独立模板参考值。
                Comp [_StencilCompOverlay] // Overlay 使用独立模板比较方式。
            } // Overlay stencil 配置结束；这里没写 Pass/Fail/ZFail，表示保持默认值。
            Blend [_ScrBlendModeOverlay] [_DstBlendModeOverlay] // Overlay 独立的混合因子；适合做发光叠加、附加色层等。
            BlendOp [_BlendOpOverlay] // Overlay 独立混合操作。
            ZWrite [_ZWrite] // Overlay 是否写深度仍受材质控制。

            HLSLPROGRAM // 进入 DrawOverlay 的 HLSL 程序体。
            // ------------------------------------- // 分隔注释。
            // Universal Pipeline keywords // URP 阴影关键字说明。
            #pragma multi_compile _MAIN_LIGHT_SHADOWS // 编译主光阴影变体。
            #pragma multi_compile _MAIN_LIGHT_SHADOWS_CASCADE // 编译级联阴影变体。
            #pragma multi_compile_fragment _SHADOWS_SOFT // 编译软阴影变体。

            // ------------------------------------- // 分隔注释。
            // Unity defined keywords // Unity 内置关键字。
            #pragma multi_compile_fog // 编译雾效变体。

            // ------------------------------------- // 分隔注释。
            // Shader Stages // 指定与主体同名的入口函数。
            #pragma vertex StarRailPassVertex // 顶点入口名与 DrawCore 相同。
            #pragma fragment StarRailPassFragment // 片元入口名与 DrawCore 相同。

            #if _DRAW_OVERLAY_ON // 条件编译：如果开启 Overlay，则编译真正的 overlay 绘制逻辑。
                #include "./SIH_StarRailInput.hlsl" // 引入同一套输入定义，保证参数/结构体一致。
                #include "./SIH_StarRailDrawCorePass.hlsl" // 直接复用核心绘制逻辑；说明 overlay 很可能只是用不同渲染状态再画一遍主体。
            #else // 如果没开启 Overlay，则编译一个“空 pass”，避免 shader 缺失入口函数。
                struct Attributes {}; // 定义空顶点输入结构；因为不会真正读取任何顶点数据。
                struct Varyings // 定义一个最小插值结构体。
                { // Varyings 结构开始。
                    float4 positionCS : SV_POSITION; // 裁剪空间位置语义；顶点阶段至少需要输出这个给栅格化器。
                }; // Varyings 结构结束。
                Varyings StarRailPassVertex(Attributes input) // 定义空顶点函数；满足编译器对入口函数的要求。
                { // 空顶点函数开始。
                    return (Varyings)0; // 返回全 0 的插值结构；因为此 pass 实际不会有有效输出。
                } // 空顶点函数结束。
                float4 StarRailPassFragment(Varyings input) : SV_TARGET // 定义空片元函数；返回一个固定颜色。
                { // 空片元函数开始。
                    return 0; // 返回全 0 颜色；配合空顶点，相当于该 pass 不做任何有效渲染。
                } // 空片元函数结束。
            #endif // Overlay 条件编译结束。

            ENDHLSL // DrawOverlay pass 程序结束。
        } // DrawOverlay pass 结束。

        Pass // 开始第六个 Pass。
        { // DrawOutline pass 块开始；用于角色描边。
            Name "DrawOutline" // Pass 名称为 DrawOutline。
            Tags // 标签块开始。
            { // 标签体开始。
                "RenderPipeline" = "UniversalPipeline" // 指定仅用于 URP。
                "RenderType" = "Opaque" // 仍归类为不透明类型。
                "LightMode" = "UniversalForwardOnly" // 指定仅在正向渲染路径中执行，且不参与额外 forward 变体。
            } // 标签结束。

            // ------------------------------------- // 分隔注释。
            // Render State Commands // 渲染状态说明。
            Cull Front // 描边 pass 剔除正面，只绘制背面；原理是将模型沿法线外扩后绘制背面，从正面看会形成轮廓边。
            ZWrite [_ZWrite] // 是否写深度由材质控制；描边一般可能写也可能不写，视项目需要。

            HLSLPROGRAM // 进入 DrawOutline HLSL 程序体。
            // ------------------------------------- // 分隔注释。
            // Unity defined keywords // Unity 内置关键字说明。
            #pragma multi_compile_fog // 编译雾效变体；让描边也受场景雾影响，避免人物边线“浮出环境”。

            // ------------------------------------- // 分隔注释。
            // Shader Stages // 指定描边 pass 顶点/片元入口。
            #pragma vertex StarRailPassVertex // 顶点入口函数名。
            #pragma fragment StarRailPassFragment // 片元入口函数名。

            /*#include "./SIH_StarRailInput.hlsl" // 被注释掉的 include；说明作者保留了直接无条件包含的旧写法。
            #include "./SIH_StarRailDrawOutlinePass.hlsl"*/ // 被注释掉的描边实现 include；现已改成条件编译版本。
            #if _OUTLINE_ON // 若描边关键字开启，则编译真正的描边实现。
                #include "./SIH_StarRailInput.hlsl" // 引入输入定义；描边需要同样的材质参数、矩阵、顶点结构等。
                #include "./SIH_StarRailDrawOutlinePass.hlsl" // 引入描边核心实现；法线外扩、宽度调节、颜色输出大概率都在这里。
            #else // 如果没开启描边，则编译空实现，保证 pass 入口合法但不产生渲染结果。
                struct Attributes {}; // 空输入结构。
                struct Varyings // 空插值结构定义开始。
                { // 结构体开始。
                    float4 positionCS : SV_POSITION; // 最小必要输出：裁剪空间位置。
                }; // 结构体结束。
                Varyings StarRailPassVertex(Attributes input) // 空描边顶点函数。
                { // 函数体开始。
                    return (Varyings)0; // 返回 0，表示不产生有效几何输出。
                } // 函数结束。
                float4 StarRailPassFragment(Varyings input) : SV_TARGET // 空描边片元函数。
                { // 函数体开始。
                    return 0; // 输出黑/0 值，相当于不绘制。
                } // 函数结束。
            #endif // 描边条件编译结束。

            ENDHLSL // DrawOutline pass 程序结束。
        } // DrawOutline pass 结束。
    } // SubShader 结束；上面定义了阴影、深度、深度法线、主体、叠加、描边等完整流程。
    FallBack "Hidden/Universal Render Pipeline/FallbackError" // 当当前 Shader 在目标平台无法工作时，退回到 URP 的错误显示 Shader，便于开发者发现问题。
} // 整个 Shader 文件结束。