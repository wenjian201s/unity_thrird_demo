#ifndef SIH_STARRAIL_DRAWOUTLINE_PASS_INCLUDED // 作用：防止这个描边 Pass 文件被重复包含。原理：使用头文件保护宏，避免函数和结构体被重复定义。
#define SIH_STARRAIL_DRAWOUTLINE_PASS_INCLUDED // 作用：标记当前文件已经被包含。原理：下次再次 include 时，上面的 #ifndef 将不成立。

// ------------------------------------- // 作用：分隔代码区域。原理：纯注释，不参与编译。
// 方法 // 作用：说明下面开始定义工具函数。原理：便于阅读逻辑结构。
// 根据相机距离调整描边宽度 // 作用：说明下面的函数用于让描边宽度适配视距和相机模式。原理：描边如果直接按世界空间外扩，远处会显得太细，近处会太粗。
float GetCameraFOV() // 作用：获取当前相机的垂直视场角 FOV。原理：从投影矩阵反推视场角。
{
    //https://answers.unity.com/questions/770838/how-can-i-extract-the-fov-information-from-the-pro.html // 作用：记录公式来源。原理：给开发者提供参考链接。
    float t = unity_CameraProjection._m11; // 作用：读取投影矩阵第 2 行第 2 列元素。原理：透视投影矩阵的 m11 与 tan(FOV/2) 成反比。
    float Rad2Deg = 180 / 3.1415; // 作用：定义弧度转角度系数。原理：atan 返回弧度，需要乘 180/π 转成角度。
    float fov = atan(1.0f / t) * 2.0 * Rad2Deg; // 作用：根据投影矩阵反推相机 FOV。原理：透视矩阵里 m11 = 1/tan(FOV/2)，所以 FOV = 2*atan(1/m11)。
    return fov; // 作用：返回计算出的视场角。原理：供后续描边宽度修正使用。
}
float ApplyOutlineDistanceFadeOut(float inputMulFix) // 作用：定义一个描边距离衰减函数。原理：当角色太远或太小时，限制描边扩张量，避免异常。
{
    //make outline "fadeout" if character is too small in camera's view // 作用：说明这个函数的设计目的。原理：相机里目标太小时，描边不应无限缩小或失真。
    return saturate(inputMulFix); // 作用：把输入限制在 0~1 范围。原理：saturate 相当于 clamp(x,0,1)，可防止数值过大或为负。
}
float GetOutlineCameraFovAndDistanceFixMultiplier(float positionVS_Z) // 作用：根据相机类型、FOV 和距离，计算描边宽度修正倍数。原理：让描边在屏幕空间中看起来更稳定。
{
    float cameraMulFix; // 作用：声明修正倍数变量。原理：后续根据透视或正交相机分别赋值。
    if(unity_OrthoParams.w == 0) // 作用：判断当前是否为透视相机。原理：Unity 中 unity_OrthoParams.w 为 0 表示透视，为 1 表示正交。
    {
        //////////////////////////////// // 作用：分隔注释。原理：纯注释。
        // Perspective camera case // 作用：说明下面处理透视相机情况。原理：透视相机下物体会随距离缩放。
        //////////////////////////////// // 作用：分隔注释。原理：纯注释。

        // keep outline similar width on screen accoss all camera distance        // 作用：说明下面先修正“随距离变化”的问题。原理：距离越远，世界空间中的同样外扩在屏幕上越细。
        cameraMulFix = abs(positionVS_Z); // 作用：以视图空间 z 深度作为宽度修正基础。原理：对象离相机越远，z 绝对值越大，需要更大的世界空间外扩才能保持屏幕宽度。

        // can replace to a tonemap function if a smooth stop is needed // 作用：说明这里可以换成更平滑的曲线。原理：当前是简单截断，不是艺术上最柔和的做法。
        cameraMulFix = ApplyOutlineDistanceFadeOut(cameraMulFix); // 作用：对距离修正值做限制。原理：防止远距离时修正倍数失控。

        // keep outline similar width on screen accoss all camera fov // 作用：说明下面修正“随 FOV 变化”的问题。原理：不同镜头焦距下，同一世界空间外扩的屏幕表现不同。
        cameraMulFix *= GetCameraFOV();       // 作用：乘以当前相机 FOV。原理：FOV 越大，物体看起来越小，需要更大的外扩补偿。
    }
    else // 作用：否则进入正交相机分支。原理：正交相机没有透视缩放，需要另一套逻辑。
    {
        //////////////////////////////// // 作用：分隔注释。原理：纯注释。
        // Orthographic camera case // 作用：说明下面处理正交相机情况。原理：正交相机物体大小主要受 ortho size 影响。
        //////////////////////////////// // 作用：分隔注释。原理：纯注释。
        float orthoSize = abs(unity_OrthoParams.y); // 作用：读取正交相机尺寸。原理：Unity 用 unity_OrthoParams.y 存正交半高或相关尺度参数。
        orthoSize = ApplyOutlineDistanceFadeOut(orthoSize); // 作用：限制 ortho size 的修正值。原理：避免过大或异常的描边宽度。
        cameraMulFix = orthoSize * 50; // 50 is a magic number to match perspective camera's outline width // 作用：把正交尺寸转换为一个经验修正倍数。原理：50 是经验常数，用来让正交和透视下的描边视觉宽度更接近。
    }

    return cameraMulFix * 0.00005; // mul a const to make return result = default normal expand amount WS // 作用：乘一个很小的常数，把修正值变成合适的世界空间外扩量。原理：前面得到的是相对倍数，最后要缩放到“法线外扩距离”的实际量级。
}

// ------------------------------------- // 作用：分隔代码区域。原理：纯注释。
// 基本结构体输入 // 作用：说明下面定义顶点输入结构。原理：约定顶点着色器接收哪些模型属性。
struct Attributes
{
    float4 positionOS   : POSITION; // 作用：输入对象空间顶点位置。原理：POSITION 语义表示模型原始顶点坐标。
    float3 normalOS     : NORMAL; // 作用：输入对象空间法线。原理：描边外扩通常沿法线方向进行。
    float4 tangentOS    : TANGENT; // 作用：输入对象空间切线。原理：若启用平滑法线描边，会用切线空间矩阵重建外扩方向。
    float4 color        : COLOR; // 作用：输入顶点色。原理：在某些二次元描边方案里，顶点色会预存平滑法线方向。
    float2 texcoord     : TEXCOORD0; // 作用：输入主 UV。原理：片元阶段可能需要按 UV 读取材质信息来决定描边颜色。
};

// ------------------------------------- // 作用：分隔代码区域。原理：纯注释。
// 基本结构体输出 // 作用：说明下面定义顶点到片元的插值结构。原理：把顶点阶段计算结果传给片元阶段。
struct Varyings
{
    float2 uv                       : TEXCOORD0; // 作用：传递 UV 坐标。原理：片元阶段继续用来采样 lightMap 或 ramp。
    float fogFactor                 : TEXCOORD1; // 作用：传递雾因子。原理：描边颜色最后也要混雾，保证和场景融合。
    float4 color                    : TEXCOORD2; // 作用：预留颜色插值通道。原理：当前代码里没实际写入使用，可能是为后续扩展保留。
    float4 positionCS               : SV_POSITION; // 作用：传递裁剪空间位置。原理：这是光栅化必须的系统语义。
};

// ------------------------------------- // 作用：分隔代码区域。原理：纯注释。
// 基本顶点着色器 // 作用：说明下面是描边 Pass 的顶点着色器。原理：在这里对模型进行“外扩”以形成描边壳。
Varyings StarRailPassVertex(Attributes input)
{
    Varyings output = (Varyings)0; // 作用：初始化输出结构体为 0。原理：防止未赋值成员携带随机值。

    VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz); // 作用：从对象空间顶点位置计算世界空间、视图空间、裁剪空间等位置数据。原理：URP 内置函数统一处理坐标变换。
    VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS, input.tangentOS); // 作用：计算世界空间法线、切线、副切线。原理：后续无论是沿法线还是沿切线空间方向扩张，都需要这些数据。

    float width = _OutlineWidth; // 作用：读取材质中的描边宽度参数。原理：这是美术可调的基础描边宽度。
    width *= GetOutlineCameraFovAndDistanceFixMultiplier(positionInputs.positionVS.z);  // 根据相机距离调整描边宽度 // 作用：结合相机视距和 FOV 修正描边宽度。原理：让描边在屏幕上的视觉厚度更加稳定。
    
    float3 positionWS = positionInputs.positionWS; // 作用：取出当前顶点的世界空间位置。原理：后续会在世界空间里直接移动顶点。
    #if _OUTLINE_VERTEX_COLOR_SMOOTH_NORMAL // 作用：如果启用了“顶点色平滑法线描边”模式。原理：有些模型会把平滑法线编码进顶点色，避免硬边模型描边断裂。
        float3x3 tbn = float3x3(normalInputs.tangentWS, normalInputs.bitangentWS, normalInputs.normalWS); // 作用：构造世界空间 TBN 矩阵。原理：把切线空间向量转换到世界空间。
        positionWS += mul(input.color.rgb * 2 - 1, tbn) * width; // 作用：使用顶点色编码的方向来外扩顶点。原理：input.color.rgb 原本是 0~1，映射到 -1~1 后可视为一个切线空间方向，再乘 TBN 变到世界空间。
    #else // 作用：否则使用普通法线描边。原理：最经典的描边方案是沿法线方向外扩。
        positionWS += normalInputs.normalWS * width; // 作用：沿世界空间法线方向外扩顶点。原理：把整个模型膨胀成一个略大的壳体，配合 Cull Front 形成外轮廓。
    #endif // 作用：结束描边外扩模式分支。原理：两种方案二选一。
    
    output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap); // 作用：变换 UV。原理：应用材质的 Tiling/Offset，保证描边 Pass 与主体采样坐标一致。
    output.fogFactor = ComputeFogFactor(positionInputs.positionCS.z); // 作用：计算雾因子。原理：根据原始裁剪空间深度估算雾混合程度。
    output.positionCS = TransformWorldToHClip(positionWS); // 作用：把外扩后的世界空间位置重新变换到裁剪空间。原理：外扩必须发生在变换完成前，否则无法真正改变轮廓。
    return output; // 作用：返回顶点着色器结果。原理：传给光栅化器和片元着色器。
}

// ------------------------------------- // 作用：分隔代码区域。原理：纯注释。
// 基本片元着色器 // 作用：说明下面是描边 Pass 的片元着色器。原理：这里不做正常光照，而是直接给描边选定一种暗色。
float4 StarRailPassFragment(Varyings input) : SV_Target
{
    float3 coolRamp = 0; // 作用：初始化冷色 Ramp 颜色。原理：后续按区域从对应 Ramp 图中采样。
    float3 warmRamp = 0; // 作用：初始化暖色 Ramp 颜色。原理：后续与 coolRamp 混合得到描边颜色。
    #if _AREA_HAIR // 作用：如果当前区域是头发。原理：头发描边颜色从头发 Ramp 里取。
        float2 outlineUV = float2(0, 0.5); // 作用：设置头发描边采样坐标。原理：x=0 取 ramp 最暗端，y=0.5 取头发 Ramp 中间那一行。
        coolRamp = SAMPLE_TEXTURE2D(_HairCoolRamp, sampler_HairCoolRamp, outlineUV).rgb; // 作用：采样头发冷色 Ramp。原理：从头发的 toon 渐变图中取适合作为描边的暗色。
        warmRamp = SAMPLE_TEXTURE2D(_HairWarmRamp, sampler_HairWarmRamp, outlineUV).rgb; // 作用：采样头发暖色 Ramp。原理：同样从暖色 ramp 取对应暗色。
    #elif _AREA_UPPERBODY || _AREA_LOWERBODY // 作用：如果是身体区域。原理：身体描边颜色需要依据材质类别决定取哪一行 Ramp。
        float4 lightMap = 0; // 作用：初始化 lightMap。原理：后续要读 lightMap.a 来确定材质枚举值。
        #if _AREA_UPPERBODY // 作用：如果是上半身。原理：采样上半身的 light map。
            lightMap = SAMPLE_TEXTURE2D(_UpperBodyLightMap, sampler_UpperBodyLightMap, input.uv); // 作用：采样上半身 lightMap。原理：其 alpha 通道记录材质枚举或行索引编码。
        #elif _AREA_LOWERBODY // 作用：如果是下半身。原理：采样下半身的 light map。
            lightMap = SAMPLE_TEXTURE2D(_LowerBodyLightMap, sampler_LowerBodyLightMap, input.uv); // 作用：采样下半身 lightMap。原理：同样通过 alpha 通道决定 ramp 行。
        #endif // 作用：结束上下半身 lightMap 采样分支。原理：二者只会取一种。
        float materialEnum = lightMap.a; // 作用：读取材质枚举值。原理：把 lightMap.a 当成“当前像素属于哪种材质分组”的编码。
        float materialEnumOffset = materialEnum + 0.0425; // 作用：给枚举值做一个偏移。原理：让量化采样更对齐到每个色带槽位中心，减少取错行的风险。
        float outlineUVy = lerp(materialEnumOffset, materialEnumOffset + 0.5 > 1 ? materialEnumOffset + 0.5 - 1 : materialEnumOffset + 0.5, fmod((round(materialEnumOffset/0.0625) - 1)/2, 2)); // 作用：根据材质枚举值计算描边应采样的 Ramp 行。原理：这和主体 Pass 的 ramp 行映射逻辑一致，通过量化和奇偶行偏移把编码值映射到实际图集排布。
        float2 outlineUV = float2(0, outlineUVy); // 作用：组合身体描边的 ramp 采样坐标。原理：x=0 取最暗端，y 由材质类型决定具体行。
        coolRamp = SAMPLE_TEXTURE2D(_BodyCoolRamp, sampler_BodyCoolRamp, outlineUV).rgb; // 作用：采样身体冷色 Ramp 的对应行。原理：取出当前材质对应的暗部颜色。
        warmRamp = SAMPLE_TEXTURE2D(_BodyWarmRamp, sampler_BodyWarmRamp, outlineUV).rgb; // 作用：采样身体暖色 Ramp 的对应行。原理：同理获取暖色版本暗部颜色。
    #elif _AREA_FACE // 作用：如果当前区域是脸。原理：脸部描边取身体 Ramp 第一行附近的固定暗色。
        float2 outlineUV = float2(0, 0.0625); // 作用：设置脸部描边的 ramp 坐标。原理：0.0625 对应 8 行图集的第一行中心附近。
        coolRamp = SAMPLE_TEXTURE2D(_BodyCoolRamp, sampler_BodyCoolRamp, outlineUV).rgb; // 作用：采样脸部冷色描边色。原理：脸部通常共用身体 Ramp 的暗部色带。
        warmRamp = SAMPLE_TEXTURE2D(_BodyWarmRamp, sampler_BodyWarmRamp, outlineUV).rgb; // 作用：采样脸部暖色描边色。原理：用于和冷色平均得到中性描边。
    #endif // 作用：结束按区域取描边色的分支。原理：不同部位的描边颜色来源不同。

    float3 ramp = lerp(coolRamp, warmRamp, 0.5); // 作用：把冷暖两套 ramp 等权混合。原理：描边通常不跟随实时光照强烈变化，而是取一个较稳定的中间暗色。
    float3 albedo = pow(saturate(ramp), _OutlineGamma);; // 作用：得到最终描边颜色。原理：先把颜色限制到 0~1，再用 gamma 指数压暗/强化对比，使描边更接近墨线感。
    
    float4 color = float4(albedo, 1); // 作用：组装最终输出颜色，alpha 固定为 1。原理：描边 Pass 默认完全不透明。
    color.rgb = MixFog(color.rgb, input.fogFactor); // 作用：给描边也混入场景雾效。原理：避免远处角色描边颜色过于突兀，与主体和环境保持一致。
    
    return color; // 作用：返回最终描边颜色。原理：写入当前渲染目标，形成模型外轮廓。
}
#endif // 作用：结束头文件保护宏。原理：与文件开头的 #ifndef / #define 配对。