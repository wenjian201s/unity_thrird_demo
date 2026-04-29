#ifndef SIH_STARRAIL_DRAWCORE_PASS_INCLUDED // 作用：防止这个头文件被重复包含。原理：使用预处理宏守卫，第一次包含时成立，后续再次包含时会跳过整段代码。
#define SIH_STARRAIL_DRAWCORE_PASS_INCLUDED // 作用：定义一个宏，标记该文件已被包含。原理：下次再次 include 时，#ifndef 条件不成立，整段代码会被跳过。

// ------------------------------------- // 作用：分隔代码区域。原理：纯注释，不参与编译。
// 方法 // 作用：说明下面开始是工具函数。原理：便于阅读代码结构。
// 去饱和度 // 作用：说明下面函数用于把彩色转换成灰度倾向。原理：通过亮度加权求和消除色相差异。
float3 desaturation(float3 color) // 作用：定义去饱和函数，输入一个 RGB 颜色。原理：返回一个三通道相同的灰度色。
{ // 作用：函数体开始。原理：HLSL 作用域开始。
    float3 grayXfer = float3(0.3, 0.59, 0.11); // 作用：定义 RGB 转灰度时的加权系数。原理：使用接近人眼亮度感知的权重，绿色贡献最大。
    float grayf = dot(color, grayXfer); // 作用：把颜色压成单个灰度值。原理：点乘等于 r*0.3 + g*0.59 + b*0.11。
    return float3(grayf, grayf, grayf); // 作用：返回灰度颜色。原理：把同一个灰度值复制到 RGB 三个通道。
} // 作用：结束 desaturation 函数。原理：函数作用域结束。

// 线性采样渐变映射 // 作用：说明下面实现一个自定义渐变采样系统。原理：用多个控制点做分段线性插值。
struct Gradient // 结构体 // 作用：定义一个渐变数据结构。原理：把渐变颜色和数量封装在一起。
{ // 作用：Gradient 结构开始。原理：结构体定义作用域开始。
    int colorsLength; // 作用：记录当前使用了多少个颜色控制点。原理：数组虽然固定长度，但只采样前 colorsLength 个。
    float4 colors[8]; // 作用：最多存 8 个颜色点。原理：rgb 存颜色，a/w 存该颜色点在渐变轴上的位置。
}; // 作用：结束结构体定义。原理：结构体声明结束。

Gradient GradientConstruct()    // 构造函数 // 作用：返回一个默认初始化的 Gradient。原理：避免每次手写完整初始化。
{ // 作用：函数体开始。原理：作用域开始。
    Gradient g; // 作用：声明一个 Gradient 变量。原理：在局部作用域中创建结构体实例。
    g.colorsLength = 2; // 作用：默认只使用两个颜色点。原理：最简单的渐变至少有起点和终点。
    g.colors[0] = float4(1, 1, 1, 0);   // 第四位是在轴上的坐标 // 作用：设置第一个颜色点为白色，位置在 0。原理：w 分量表示渐变轴坐标。
    g.colors[1] = float4(1, 1, 1, 1); // 作用：设置第二个颜色点为白色，位置在 1。原理：默认形成一条全白渐变。
    g.colors[2] = float4(0, 0, 0, 0); // 作用：清零未使用的颜色点。原理：避免脏数据影响调试或后续逻辑。
    g.colors[3] = float4(0, 0, 0, 0); // 作用：同上。原理：显式初始化。
    g.colors[4] = float4(0, 0, 0, 0); // 作用：同上。原理：显式初始化。
    g.colors[5] = float4(0, 0, 0, 0); // 作用：同上。原理：显式初始化。
    g.colors[6] = float4(0, 0, 0, 0); // 作用：同上。原理：显式初始化。
    g.colors[7] = float4(0, 0, 0, 0); // 作用：同上。原理：显式初始化。
    return g; // 作用：返回初始化完成的渐变结构。原理：按值返回结构体。
} // 作用：结束 GradientConstruct 函数。原理：函数作用域结束。

float3 SampleGradient(Gradient Gradient, float Time)    // 方法 // 作用：定义渐变采样函数，按 Time 从渐变中取色。原理：在各个控制点之间做线性插值。
{ // 作用：函数体开始。原理：作用域开始。
    float3 color = Gradient.colors[0].rgb; // 作用：把结果初始化为第一个颜色点。原理：当 Time 很小时默认落在起始颜色。
    for (int c = 1; c < Gradient.colorsLength; c++) // 作用：遍历每个后续颜色控制点。原理：逐段构建分段线性渐变。
    { // 作用：for 循环体开始。原理：每一段都独立计算插值比例。
        float colorPos = saturate((Time - Gradient.colors[c- 1 ].w) / (Gradient.colors[c].w - Gradient.colors[c - 1].w)) * step(c, Gradient.colorsLength - 1); // 作用：计算 Time 在当前两控制点之间的归一化位置。原理：先做区间映射，再用 saturate 限制到 0~1，step 用于保护末尾索引。
        color = lerp(color, Gradient.colors[c].rgb, colorPos); // 作用：在当前结果和第 c 个颜色点之间插值。原理：通过分段 lerp 逐步推进到 Time 所在颜色。
    } // 作用：结束 for 循环体。原理：当前所有渐变段处理完毕。
    #ifdef UNITY_COLORSPACE_GAMMA // 作用：判断当前是否工作在 Gamma 色彩空间。原理：不同色彩空间下显示结果不同，需要做转换。
        color = LinearToSRGB(color); // 作用：把线性空间颜色转成 sRGB。原理：保证 Gamma 工作流下视觉结果正确。
    #endif // 作用：结束 Gamma 条件编译。原理：只有在对应宏开启时才编译上面一行。
    return color; // 作用：返回最终渐变颜色。原理：输出 float3 供后续着色使用。
} // 作用：结束 SampleGradient 函数。原理：函数作用域结束。

// ------------------------------------- // 作用：分隔代码区域。原理：纯注释。
// 基本结构体输入 // 作用：说明下面定义顶点输入结构。原理：约定顶点着色器输入格式。
struct Attributes // 作用：定义顶点输入结构。原理：把模型顶点属性按语义打包传入顶点着色器。
{ // 作用：结构体开始。原理：定义成员作用域。
    float4 positionOS   : POSITION; // 作用：输入对象空间顶点位置。原理：POSITION 语义告诉 GPU 这是顶点坐标。
    float3 normalOS     : NORMAL; // 作用：输入对象空间法线。原理：NORMAL 语义用于法线变换和光照计算。
    float4 tangentOS    : TANGENT; // 作用：输入对象空间切线。原理：切线常用于构建 TBN 或法线相关辅助计算。
    float2 texcoord     : TEXCOORD0; // 作用：输入主 UV 坐标。原理：供纹理采样使用。
}; // 作用：结束 Attributes 结构。原理：结构体定义结束。

// ------------------------------------- // 作用：分隔代码区域。原理：纯注释。
// 基本结构体输出 // 作用：说明下面定义顶点到片元的插值结构。原理：把顶点阶段算好的数据传给片元阶段。
struct Varyings // 作用：定义顶点着色器输出/片元着色器输入结构。原理：经过光栅化后自动插值。
{ // 作用：结构体开始。原理：定义成员作用域。
    float2 uv                       : TEXCOORD0; // 作用：传递 UV 到片元着色器。原理：插值后用于纹理采样。
    float4 positionWSAndFogFactor   : TEXCOORD1;    // xyz: positionWS, w: vertex FogFactor // 作用：传递世界坐标和雾因子。原理：把三个世界坐标和一个 fog 值打包到一个 float4。
    float3 normalWS                 : TEXCOORD2; // 作用：传递世界空间法线。原理：供片元阶段做光照计算。
    float3 viewDirectionWS          : TEXCOORD3; // 作用：传递世界空间视线方向。原理：供高光、边缘光等依赖视角的效果使用。
    float3 SH                       : TEXCOORD4;     // 作用：传递球谐环境光结果。原理：把顶点阶段采样到的间接光插值到片元。
    float4 positionCS               : SV_POSITION; // 作用：传递裁剪空间位置。原理：这是光栅化所必需的系统语义。
}; // 作用：结束 Varyings 结构。原理：结构体定义结束。

// ------------------------------------- // 作用：分隔代码区域。原理：纯注释。
// 基本顶点着色器 // 作用：说明下面是顶点着色器主函数。原理：负责把模型顶点变换到渲染需要的空间。
Varyings StarRailPassVertex(Attributes input) // 作用：定义顶点着色器入口。原理：输入 Attributes，输出 Varyings。
{ // 作用：函数体开始。原理：顶点逻辑开始。
    Varyings output = (Varyings)0; // 作用：把输出结构体清零初始化。原理：避免未赋值成员带来随机值。

    VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz); // 作用：从对象空间位置计算多种位置空间信息。原理：URP 内置函数会生成世界空间、裁剪空间等结果。
    VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS, input.tangentOS); // 作用：从对象空间法线和切线计算法线相关数据。原理：URP 内置函数会完成法线变换。
    
    output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap); // 作用：根据材质的 Tiling/Offset 变换 UV。原理：TRANSFORM_TEX 会套用 _BaseMap_ST 参数。
    output.positionWSAndFogFactor = float4(positionInputs.positionWS, ComputeFogFactor(positionInputs.positionCS.z)); // 作用：打包世界坐标和雾因子。原理：雾因子根据裁剪空间 z 计算，世界坐标供片元阶段继续用。
    output.normalWS = normalInputs.normalWS; // 作用：传递世界空间法线。原理：法线要在世界空间下与光向量做点乘。
    output.viewDirectionWS = unity_OrthoParams.w == 0 ? GetCameraPositionWS() - positionInputs.positionWS : GetWorldToViewMatrix()[2].xyz; // 作用：计算视线方向。原理：透视相机用相机位置减像素位置，正交相机直接取视图矩阵前向轴。
    output.SH = SampleSH(lerp(normalInputs.normalWS, float3(0,0,0), _IndirectLightFlattenNormal)); // 作用：采样球谐环境光。原理：先把法线按参数向零向量压平，减弱法线细节对间接光的影响，再用 SampleSH 取环境光。
    output.positionCS = positionInputs.positionCS; // 作用：输出裁剪空间位置。原理：给 GPU 做后续裁剪和光栅化。
    
    return output; // 作用：返回顶点着色器输出。原理：传给后续插值和片元着色器。
} // 作用：结束顶点着色器。原理：函数作用域结束。

// ------------------------------------- // 作用：分隔代码区域。原理：纯注释。
// 基本片元着色器 // 作用：说明下面是片元着色器主函数。原理：负责对每个像素计算最终颜色。
float4 StarRailPassFragment(Varyings input, bool isFrontFace : SV_IsFrontFace) : SV_Target // 作用：定义片元着色器入口。原理：输入插值结果和正反面标记，输出目标颜色。
{ // 作用：函数体开始。原理：片元逻辑开始。
    // Vector // 作用：说明下面是向量和基础光照数据准备。原理：先整理后续计算所需的方向信息。
    float3 positionWS = input.positionWSAndFogFactor.xyz; // 作用：取出世界空间位置。原理：前面打包在 TEXCOORD1.xyz。
    float4 shadowCoord = TransformWorldToShadowCoord(positionWS); // 作用：把世界坐标变换到阴影贴图坐标。原理：用于查询主光阴影。
    Light mainLight = GetMainLight(shadowCoord); // 作用：获取主光源信息。原理：URP 会返回方向、颜色、阴影衰减等数据。
    float3 lightDirectionWS = normalize(mainLight.direction); // 作用：归一化主光方向。原理：点乘和半角向量都要求单位向量。
    float3 normalWS = normalize(input.normalWS); // 作用：归一化世界法线。原理：插值后法线长度不一定为 1。
    float3 viewDirectionWS = normalize(input.viewDirectionWS); // 作用：归一化视线方向。原理：后续高光和边缘光依赖单位方向。

    float2 baseUV = input.uv;

    // 正面使用贴图下半部分：0.0 ~ 0.5
    // 背面使用贴图上半部分：0.5 ~ 1.0
    float2 doubleSideUV = baseUV;
    doubleSideUV.y = baseUV.y * 0.5 + (isFrontFace ? 0.0 : 0.5);
    
    // BaseMap // 作用：说明下面是基础颜色与区域贴图采样。原理：按不同部位切换不同颜色图。
    float3 baseColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, doubleSideUV).rgb; // 作用：默认先采样主基础贴图。原理：作为所有区域的兜底底色。
    float4 areaMap = 0; // 作用：声明一个区域贴图变量。原理：后续按区域采样，顺便复用其 alpha 等通道。
    #if _AREA_FACE // 作用：如果当前材质区域是脸部。原理：由 shader keyword 控制编译分支。
        areaMap = SAMPLE_TEXTURE2D(_FaceColorMap, sampler_FaceColorMap, input.uv); // 作用：采样脸部颜色图。原理：脸部通常有单独贴图与身体分离。
        baseColor = areaMap.rgb; // 作用：用脸部贴图覆盖默认底色。原理：该区域以专用贴图为准。
    #elif _AREA_HAIR  // 作用：否则如果是头发区域。原理：互斥材质区域分支。
        areaMap = SAMPLE_TEXTURE2D(_HairColorMap, sampler_HairColorMap, input.uv); // 作用：采样头发颜色图。原理：头发颜色和身体往往分开控制。
        baseColor = areaMap.rgb; // 作用：用头发贴图覆盖底色。原理：该区域使用专属颜色。
    #elif _AREA_UPPERBODY // 作用：否则如果是上半身区域。原理：区域分支选择。
        areaMap = SAMPLE_TEXTURE2D(_UpperBodyColorMap, sampler_UpperBodyColorMap, input.uv); // 作用：采样上半身颜色图。原理：上衣等部分单独纹理。
        baseColor = areaMap.rgb; // 作用：用上半身贴图覆盖底色。原理：区域专属底色。
    #elif _AREA_LOWERBODY  // 作用：否则如果是下半身区域。原理：区域分支选择。
        areaMap = SAMPLE_TEXTURE2D(_LowerBodyColorMap, sampler_LowerBodyColorMap, input.uv); // 作用：采样下半身颜色图。原理：裤/裙/腿单独纹理。
        baseColor = areaMap.rgb; // 作用：用下半身贴图覆盖底色。原理：区域专属底色。
    #endif // 作用：结束区域底色分支。原理：只保留当前编译区域对应代码。
    baseColor.rgb *= lerp(_BackFaceTintColor, _FrontFaceTintColor, isFrontFace);    // 双面颜色相乘 // 作用：根据正反面给底色乘不同染色。原理：SV_IsFrontFace 为真时取正面色，否则取背面色，常用于发片双面差异。

    // LightMap // 作用：说明下面是光照辅助贴图采样。原理：不同区域会有单独的 light map 存 AO、材质 mask 等信息。
    float4 lightMap = 0; // 作用：初始化 lightMap。原理：默认全 0，脸部分支可能根本不用它。
    #if _AREA_HAIR || _AREA_UPPERBODY || _AREA_LOWERBODY // 作用：只有头发/身体区域才使用 light map。原理：脸部通常走特殊 faceMap 逻辑。
    {
        #if _AREA_HAIR // 作用：如果当前是头发区域。原理：头发用专属 light map。
            lightMap = SAMPLE_TEXTURE2D(_HairLightMap, sampler_HairLightMap, input.uv); // 作用：采样头发 light map。原理：其各通道通常存 AO、阴影阈值、高光 mask 等。
        #elif _AREA_UPPERBODY // 作用：如果当前是上半身区域。原理：不同身体区域可存不同控制图。
            lightMap = SAMPLE_TEXTURE2D(_UpperBodyLightMap, sampler_UpperBodyLightMap, input.uv); // 作用：采样上半身 light map。原理：供阴影和高光使用。
        #elif _AREA_LOWERBODY // 作用：如果当前是下半身区域。原理：区域独立控制。
            lightMap = SAMPLE_TEXTURE2D(_LowerBodyLightMap, sampler_LowerBodyLightMap, input.uv); // 作用：采样下半身 light map。原理：同样提供 AO、mask 等辅助信息。
        #endif // 作用：结束具体区域 lightMap 选择。原理：只编译当前区域需要的采样。
    }
    #endif // 作用：结束 lightMap 条件编译。原理：脸部区域不会走这里。

    // FaceMap // 作用：说明下面采样脸部专用贴图。原理：脸部阴影和五官遮罩需要额外控制图。
    float4 faceMap = 0; // 作用：初始化 faceMap。原理：非脸部区域默认不用。
    #if _AREA_FACE // 作用：只有脸部区域才采样。原理：减少无用采样。
        faceMap = SAMPLE_TEXTURE2D(_FaceMap, sampler_FaceMap, input.uv); // 作用：采样脸部控制图。原理：通常其中不同通道存嘴内 AO、假描边、SDF 等。
    #endif // 作用：结束脸部贴图分支。原理：非脸部区域不编译该采样。

    // IndirectLightColor // 作用：说明下面计算间接光颜色。原理：使用球谐环境光再叠加 AO 和底色混合。
    float3 indirectLightColor = input.SH * _IndirectLightUsage; // 作用：得到基础间接光。原理：球谐光 SH 近似环境漫反射，再乘总权重。
    #if _AREA_HAIR || _AREA_UPPERBODY || _AREA_LOWERBODY // 作用：头发和身体区域走 lightMap AO。原理：lightMap.r 被当成 AO 使用。
        indirectLightColor *= lerp(1, lightMap.r, _IndirectLightOcclusionUsage);    // 头发身体AO相乘 // 作用：按参数决定间接光受 AO 影响多少。原理：在 1 和 AO 值之间插值后相乘。
    #else // 作用：否则走脸部 AO 逻辑。原理：脸部 AO 不直接用 body lightMap。
        float faceMask = lerp(faceMap.g, 1, step(faceMap.r, 0.5)); // 作用：生成脸部 AO mask。原理：当 faceMap.r 小于等于 0.5 时用 g 通道，否则用 1；相当于把某些区域单独排除或替换。
        indirectLightColor *= lerp(1, faceMask, _IndirectLightOcclusionUsage);    // 嘴巴内都是AO区域 // 作用：让间接光受脸部 mask 影响。原理：嘴内等区域会更暗，形成口腔 AO。
    #endif // 作用：结束间接光 AO 分支。原理：按区域使用不同 AO 来源。
    indirectLightColor *= lerp(1, baseColor.rgb, _IndirectLightMixBaseColor); // 作用：让间接光混入底色。原理：避免环境光过灰，增强角色色彩统一性。

    // MainLightShadow & Ramp // 作用：说明下面计算主光阴影和 Ramp 映射。原理：将主光明暗映射到 toon 渐变。
    float3 mianLightColor = lerp(desaturation(mainLight.color), mainLight.color, _MainLightColorUsage); // 插值去饱和减弱光颜色的影响 // 作用：得到主光颜色。原理：在灰化的灯光色和原始灯光色之间插值，控制灯光色对角色染色程度。
    float mainLightShadow = 1; // 作用：初始化主光阴影因子。原理：默认全亮，后面再按区域计算。
    int rampRowIndex = 0; // 作用：初始化 Ramp 所在行号。原理：Ramp 贴图可能被分成多行。
    int rampRowNum = 1; // 头发1行，身体8行 // 作用：初始化 Ramp 总行数。原理：头发只用 1 行，身体/脸会用 8 行或更多变化。
    #if _AREA_HAIR || _AREA_UPPERBODY || _AREA_LOWERBODY // 作用：头发和身体区域走普通 Lambert + 控制图阴影。原理：这些区域不走脸部专用 SDF。
        // Lambert // 作用：说明下面使用 Lambert 基础明暗。原理：N·L 决定漫反射明暗。
        float NoL = dot(normalWS, lightDirectionWS); // 作用：计算法线和光方向点积。原理：Lambert 漫反射基础项。
        float remappedNoL = NoL * 0.5 + 0.5; // 作用：把 -1~1 的范围映射到 0~1。原理：方便和控制贴图阈值比较。
        mainLightShadow = smoothstep(1 - lightMap.g + _ShadowThresholdCenter - _ShadowThresholdSoftness, // 作用：计算阴影下边界。原理：用 lightMap.g 和全局阈值控制阴影线位置。
                                     1 - lightMap.g + _ShadowThresholdCenter + _ShadowThresholdSoftness, // 作用：计算阴影上边界。原理：加减 softness 形成平滑过渡带。
                                     remappedNoL); // 作用：输出平滑阴影结果。原理：smoothstep 会在阈值附近形成软边 toon 阴影。
        mainLightShadow *= lightMap.r; // 作用：再乘一次 AO/亮度 mask。原理：让某些区域即便受光也能保持压暗。

        // Ramp // 作用：说明下面选择 Ramp 的具体行。原理：不同区域或材质编码选择不同色带。
        #if _AREA_HAIR // 作用：头发区域只用一行 Ramp。原理：头发的色带更简单。
            rampRowIndex = 0; // 作用：头发固定使用第 0 行。原理：单行 ramp 无需切换。
            rampRowNum = 1; // 作用：头发 Ramp 贴图按 1 行处理。原理：计算 UV 时使用单行。
        #elif _AREA_UPPERBODY || _AREA_LOWERBODY // 作用：身体区域根据贴图 alpha 选择行。原理：不同材质部分映射到不同 Ramp 行。
            int rawIndex = (round((lightMap.a + 0.0425) / 0.0625) - 1) / 2;    // 将灰度值转换成没调整过的行序号 // 作用：把 lightMap.a 量化成行号索引。原理：按固定步长把灰度映射到离散槽位。
            rampRowIndex = lerp(rawIndex, rawIndex + 4 < 8 ? rawIndex + 4 : rawIndex + 4 - 8, fmod(rawIndex, 2));   // 判断行序号是奇数还是偶数，偶数不用调整，奇数偏移4行，一个周期是8，超过8就减8 // 作用：根据奇偶性重新排列行号。原理：把编码过的行序映射到实际 Ramp 图中的排列方式。
        #endif // 作用：结束身体/头发 Ramp 行选择。原理：不同区域走不同映射方式。
    #elif _AREA_FACE // 作用：脸部区域走 SDF 阴影。原理：脸部不能直接用法线 Lambert，否则阴影会乱跑。
        // SDF // 作用：说明下面是脸部 SDF 阴影逻辑。原理：通过头部局部坐标和 faceMap.a 的 SDF 生成稳定脸影。
        float3 headForward = normalize(_HeadForward); // 作用：归一化头部前向量。原理：作为脸部局部坐标基之一。
        float3 headRight = normalize(_HeadRight); // 作用：归一化头部右向量。原理：作为脸部局部坐标基之一。
        float3 headUp = normalize(cross(headForward, headRight));  // Unity 是左手坐标系，前向量和右向量得到向上向量 // 作用：求头部上向量。原理：用叉乘构建局部正交基，用于把光投影到脸部平面。
    
        float3 fixedLightDirectionWS = normalize(lightDirectionWS - dot(lightDirectionWS, headUp) * headUp);    // 把光向量投影倒头坐标系的水平面，不然人物颠倒过来阴影是反的 // 作用：去掉光方向在 headUp 上的分量。原理：把光投影到脸部水平面，避免头部上下翻转时脸影错误。
        float2 sdfUV = float2(sign(dot(fixedLightDirectionWS, headRight)), 1) * input.uv * float2(-1, 1);   // 判断光照在脸左还是右，正数是脸左，复数是脸右，图是左黑右白 // 作用：根据光是从左还是右照来决定是否镜像采样 UV。原理：通过 headRight 点乘符号切换左右脸共用一张对称 SDF 图。
        float sdfValue = SAMPLE_TEXTURE2D(_FaceMap, sampler_FaceMap, sdfUV).a;   //采样SDF图 // 作用：从 faceMap 的 alpha 通道采样 SDF 值。原理：alpha 通道被用作脸部阴影分布图。
        sdfValue += _FaceShadowOffset;  // 让正面不是全白，偏移一点点 // 作用：给 SDF 值整体加偏移。原理：微调阴影线，避免正面全亮太死板。
    
        float sdfThreshold = 1 - (dot(fixedLightDirectionWS, headForward) * 0.5 + 0.5);   // 从-1~1映射到0~1，再反向一下，照正面是0，背面是1 // 作用：生成与光照方向相关的阈值。原理：光越接近正前方阈值越低，越容易点亮脸部。
        float sdf = smoothstep(sdfThreshold- _FaceShadowTransitionSoftness, sdfThreshold + _FaceShadowTransitionSoftness, sdfValue);   // 光在正前方阈值越低，越容易被点亮，像素的灰度值超过阈值时会被点亮 // 作用：根据 SDF 值和阈值计算脸部明暗。原理：smoothstep 形成可控软边的脸影过渡。
        mainLightShadow = lerp(faceMap.g, sdf, step(faceMap.r, 0.5));  // 把遮罩外的五官替换成AO // 作用：混合 SDF 阴影和 faceMap.g。原理：部分五官区域不直接受 SDF 控制，而是用专门 AO 遮罩替代。

        // Ramp // 作用：说明下面设置脸部使用的 Ramp 行。原理：脸部可能复用身体 Ramp 图的多行。
        rampRowIndex = 0; // 作用：脸部固定从第 0 行开始。原理：具体只用一行入口。
        rampRowNum = 8; // 作用：脸部 Ramp 按 8 行图集处理。原理：用于后面按图集方式计算 UV。
    #endif // 作用：结束主光阴影区域分支。原理：身体与脸部使用不同阴影模型。

    // RampMap // 作用：说明下面根据阴影结果采样 Ramp 颜色。原理：将 mainLightShadow 映射到 toon 色带纹理。
    float rampUVx = mainLightShadow * (1 - _ShadowRampOffset) + _ShadowRampOffset;  // 细节集中在3/4的地方，挤压一下 // 作用：计算 Ramp 的横向 UV。原理：通过 offset 压缩有效采样区，使色带细节集中在右侧。
    float rampUVy = (2 * rampRowIndex + 1) * (1.0 / (rampRowNum * 2));    // 先将行序号改为半行序号，再乘以半行宽度 // 作用：计算 Ramp 的纵向 UV。原理：取每一行的中心位置，避免采样到行与行边界。
    float2 rampUV = float2(rampUVx, rampUVy); // 作用：组合 Ramp 采样坐标。原理：供后续 cool/warm ramp 纹理采样。
    float3 coolRamp = 1; // 作用：初始化冷色 Ramp。原理：默认白色不影响结果。
    float3 warmRamp = 1; // 作用：初始化暖色 Ramp。原理：默认白色不影响结果。
    #if _AREA_HAIR // 作用：头发区域使用头发专属 Ramp。原理：头发与身体的色带风格不同。
        coolRamp = SAMPLE_TEXTURE2D(_HairCoolRamp, sampler_HairCoolRamp, rampUV).rgb; // 作用：采样头发冷色 Ramp。原理：阴影偏冷时使用。
        warmRamp = SAMPLE_TEXTURE2D(_HairWarmRamp, sampler_HairWarmRamp, rampUV).rgb; // 作用：采样头发暖色 Ramp。原理：暖光环境下使用。
    #elif _AREA_FACE || _AREA_UPPERBODY || _AREA_LOWERBODY // 作用：脸和身体使用身体 Ramp。原理：共用一套色带纹理。
        coolRamp = SAMPLE_TEXTURE2D(_BodyCoolRamp, sampler_BodyCoolRamp, rampUV).rgb; // 作用：采样身体冷色 Ramp。原理：提供冷调 toon 色带。
        warmRamp = SAMPLE_TEXTURE2D(_BodyWarmRamp, sampler_BodyWarmRamp, rampUV).rgb; // 作用：采样身体暖色 Ramp。原理：提供暖调 toon 色带。
    #endif // 作用：结束 Ramp 采样分支。原理：不同区域选择不同贴图。
    float isDay = lightDirectionWS.y * 0.5 + 0.5;   // 光向量的数坐标插值冷Ramp 和 暖Ramp // 作用：根据光方向的 y 值估算冷暖倾向。原理：y 越高越像上方日光，越偏向 warmRamp。
    float3 rampColor = lerp(coolRamp, warmRamp, isDay); // 作用：在冷暖 Ramp 之间插值。原理：模拟不同光环境的色温变化。
    mianLightColor *= baseColor.rgb * rampColor; // 作用：把主光颜色乘上底色和 Ramp 色带。原理：得到最终 toon 主光颜色。

    // Specular // 作用：说明下面计算高光。原理：基于 Blinn-Phong，并区分金属/非金属。
    float3 specularColor = 0; // 作用：初始化高光颜色。原理：默认没有高光。
    #if _AREA_HAIR || _AREA_UPPERBODY || _AREA_LOWERBODY // 作用：只有头发和身体算高光。原理：脸部通常避免这种硬高光。
        float3 halfVectorWS = normalize(viewDirectionWS + lightDirectionWS); // 作用：计算半角向量。原理：Blinn-Phong 用视线和光线的角平分方向求高光。
        float NoH = dot(normalWS, halfVectorWS); // 作用：计算法线和半角向量点积。原理：高光强度基础项。
        float blinnPhong = pow(saturate(NoH), _SpecularExpon); // 作用：计算 Blinn-Phong 高光值。原理：指数越大，高光越尖锐。
    
        float nonMetalSpecular = step(1.04 - blinnPhong, lightMap.b) * _SpecularKsNonMetal;   // blinnPhong反向与阈值图比较，偏移一点避免漏光。再乘以反射率，非金属的反射率固定是0.04 // 作用：计算非金属高光。原理：把高光值当作阈值与 lightMap.b 比较，做出离散 toon 式高光，再乘非金属 F0。
        float metalSpecular = blinnPhong * lightMap.b * _SpecularKsMetal; // 作用：计算金属高光。原理：金属直接使用连续高光并受 mask 和金属反射率调制。

        float metallic = 0; // 作用：初始化金属度。原理：默认不是金属。
        #if _AREA_UPPERBODY || _AREA_LOWERBODY // 作用：身体区域进一步根据贴图区分金属。原理：衣物配件可能有金属部分。
            metallic = saturate((abs(lightMap.a - _SpecularMetalRange) - 0.1) / (0 - 0.1));   // 贴图的0.52正好是金属度,0.1作为插值范围 // 作用：根据 lightMap.a 判断该像素是否接近金属编码值。原理：以 _SpecularMetalRange 为中心做一个软阈值。
        #endif // 作用：结束金属度分支。原理：头发不使用这套金属判断。
    
        specularColor = lerp(nonMetalSpecular, metalSpecular * baseColor.rgb, metallic); // 作用：按 metallic 在非金属和金属高光间插值。原理：金属高光会染上底色，非金属高光偏白。
        specularColor *= mainLight.color; // 作用：再乘主光颜色。原理：高光应受光源颜色影响。
        specularColor *= _SpecularBrightness; // 作用：乘全局高光亮度。原理：方便整体调节高光强弱。
    #endif // 作用：结束高光条件编译。原理：脸部不进入该逻辑。

    // Stockings // 作用：说明下面是丝袜/半透布料效果。原理：基于视角和渐变颜色做透感变化。
    float3 stockingsEffect = 1; // 作用：初始化丝袜效果为 1。原理：后续作为乘色因子，不启用时不改变 albedo。
    #if _AREA_UPPERBODY || _AREA_LOWERBODY // 作用：只有身体区域可能有丝袜/布料效果。原理：脸和头发通常不需要。
        float2 stockingsMapRG = 0; // 作用：初始化丝袜贴图的 RG 数据。原理：通常 r 存启用 mask，g 存厚度/亮区控制。
        float stockingsMapB = 0; // 作用：初始化丝袜贴图的 B 数据。原理：b 通道会高频采样做细节扰动。
        #if _AREA_UPPERBODY // 作用：上半身区域采样上半身丝袜图。原理：支持不同区域单独控制。
            stockingsMapRG = SAMPLE_TEXTURE2D(_UpperBodyStockings, sampler_UpperBodyStockings, input.uv).rg; // 作用：采样基础 RG 控制信息。原理：低频控制整体分布。
            stockingsMapB = SAMPLE_TEXTURE2D(_UpperBodyStockings, sampler_UpperBodyStockings, input.uv * 20).b; // 作用：高频采样 B 通道细节。原理：放大 UV 产生细密纹理感。
        #elif _AREA_LOWERBODY // 作用：下半身区域采样下半身丝袜图。原理：腿部通常是丝袜主要使用区域。
            stockingsMapRG = SAMPLE_TEXTURE2D(_LowerBodyStockings, sampler_LowerBodyStockings, input.uv).rg; // 作用：采样下半身丝袜 RG 控制图。原理：控制覆盖范围和厚薄。
            stockingsMapB = SAMPLE_TEXTURE2D(_LowerBodyStockings, sampler_LowerBodyStockings, input.uv * 20).b; // 作用：高频采样 B 通道。原理：为表面增加细节变化。
        #endif // 作用：结束具体区域丝袜图采样。原理：按区域取对应纹理。

        float NoV = dot(normalWS, viewDirectionWS); // 作用：计算法线与视线夹角。原理：丝袜亮暗通常强烈依赖观察角度。
        float fac = NoV; // 作用：以 NoV 作为初始控制值。原理：面向相机和掠射角区域会有不同视觉效果。
        fac = pow(saturate(fac), _StockingsTransitionPower); // 作用：调整视角过渡曲线。原理：pow 可以压缩或强化亮暗变化速度。
        fac = saturate((fac - _StockingsTransitionHardness / 2) / (1- _StockingsTransitionHardness));   // 亮暗过渡的硬度 // 作用：进一步控制过渡边界硬度。原理：重新映射到 0~1 并压缩过渡区间。
        fac = fac * (stockingsMapB * _StockingsTextureUsage + (1 - _StockingsTextureUsage));    // 混入细节纹理 // 作用：把高频纹理细节混进 fac。原理：通过 textureUsage 在纯平滑和带细节之间插值。
        fac = lerp(fac, 1, stockingsMapRG.g);   // 厚度插值一下亮区 // 作用：根据 g 通道把某些区域推向更亮。原理：模拟薄料或高透区域。
    
        Gradient curve = GradientConstruct(); // 作用：构造一个默认渐变。原理：后面重写成丝袜三段色渐变。
        curve.colorsLength = 3; // 作用：使用 3 个颜色点。原理：形成暗色-过渡色-亮色的三段式变化。
        curve.colors[0] = float4(_StockingsDarkColor.rgb, 0); // 作用：设置渐变起点为丝袜暗部颜色。原理：fac=0 时输出暗色。
        curve.colors[1] = float4(_StockingsTransitionColor.rgb, _StockingsTransitionThreshold); // 作用：设置中间过渡色和阈值位置。原理：在指定位置出现中间色带。
        curve.colors[2] = float4(_StockingsLightColor.rgb, 1); // 作用：设置终点为亮部颜色。原理：fac=1 时输出高亮色。
        float3 stockingsColor = SampleGradient(curve, fac); // 作用：按 fac 从渐变中采样丝袜颜色。原理：通过分段 lerp 得到平滑变化。
        
        stockingsEffect = lerp(1, stockingsColor, stockingsMapRG.r);  // 作用：按 r 通道决定丝袜效果混合强度。原理：r 可视为丝袜覆盖 mask。
    #endif // 作用：结束丝袜条件编译。原理：非身体区域不参与该效果。

    // 边缘光 // 作用：说明下面计算 Rim Light。原理：通过比较自身深度和偏移位置的场景深度检测轮廓边缘。
    float linearEyeDepth = LinearEyeDepth(input.positionCS.z, _ZBufferParams); // 作用：把当前像素深度转成线性眼空间深度。原理：方便和场景深度做可比的差值。
    float3 normalVS = mul((float3x3)UNITY_MATRIX_V, normalWS); // 作用：把法线从世界空间变到视图空间。原理：视图空间下 x 方向可以判断左右轮廓。
    float2 uvOffset = float2(sign(normalVS.x), 0) * _RimLightWidth / (1 + linearEyeDepth) / 100;    // 法线的横坐标确定采样UV的偏移方向，乘偏移量，除以深度实现近粗远细，加1限制最大宽度 // 作用：计算深度采样偏移量。原理：左右边缘只沿 x 偏移，并随距离变远而变细。
    int2 loadTexPos = input.positionCS.xy + uvOffset * _ScaledScreenParams.xy;   // 采样深度缓冲，把UV偏移转换成坐标偏移 // 作用：把归一化偏移转成屏幕像素坐标偏移。原理：LoadSceneDepth 需要整数屏幕位置。
    loadTexPos = min(loadTexPos, _ScaledScreenParams.xy - 1); // 作用：限制采样坐标不越界。原理：避免访问屏幕外深度纹理。
    float offsetSceneDepth = LoadSceneDepth(loadTexPos);   // 在深度缓存上采样偏移像素的深度 // 作用：取偏移位置的场景深度。原理：用于检测该方向是否马上遇到背景或别的物体。
    float offsetLinearEyeDepth = LinearEyeDepth(offsetSceneDepth, _ZBufferParams);  // 将非线性的深度缓存转换成线性的 // 作用：把偏移位置深度也转成线性值。原理：和当前像素深度保持同一度量标准。
    float rimLight = saturate(offsetLinearEyeDepth - (linearEyeDepth + _RimLightThreshold)) / _RimLightFadeout; // 作用：计算边缘光强度。原理：偏移点如果明显更远，说明当前像素靠近轮廓边界，就点亮 rim。
    float3 rimLightColor = rimLight * mainLight.color.rgb; // 作用：用主光颜色给边缘光染色。原理：让边缘光和光源色保持一致。
    rimLightColor *= _RimLightTintColor; // 作用：再乘额外 tint。原理：允许艺术上独立调整 rim 颜色。
    rimLightColor *= _RimLightBrightness; // 作用：乘亮度系数。原理：统一控制 rim 强度。

    // 自发光 // 作用：说明下面计算 emission。原理：让某些区域不依赖受光直接发亮。
    float3 emissionColor = 0; // 作用：初始化自发光为 0。原理：默认关闭。
    #if _EMISSION_ON // 作用：只有开了 emission keyword 才编译。原理：减少无用指令。
        emissionColor = areaMap.a; // 作用：取区域贴图 alpha 作为发光遮罩。原理：alpha 通道被复用为 emission mask。
        emissionColor *= lerp(1, baseColor, _EmissionMixBaseColor); // 作用：决定发光是否混合底色。原理：0 时纯遮罩，1 时带上材质本身颜色。
        emissionColor *= _EmissionTintColor; // 作用：乘自发光染色。原理：统一控制发光色相。
        emissionColor *=_EmissionIntensity; // 作用：乘发光强度。原理：控制最终亮度。
    #endif // 作用：结束自发光条件编译。原理：关闭时完全不参与。
            

    // 脸部描边 // 作用：说明下面是脸部专用假描边。原理：不是几何描边，而是根据 mask 和视角在脸上叠一层深色。
    float fakeOutlineEffect = 0; // 作用：初始化假描边强度。原理：默认不生效。
    float3 fakeOutlineColor = 0; // 作用：初始化假描边颜色。原理：默认无颜色。
    #if _AREA_FACE && _OUTLINE_ON // 作用：只有脸部且开了描边时才编译。原理：该效果专用于脸。
        float fakeOutline = faceMap.b; // 作用：取 faceMap.b 作为假描边遮罩。原理：b 通道被用来标记需要脸描边的位置。
        float3 headForwardShadow = normalize(_HeadForward); // 作用：归一化头前方向。原理：用于判断视角是否接近正脸。
        fakeOutlineEffect = smoothstep(0.0, 0.25, pow(saturate(dot(headForwardShadow, viewDirectionWS)), 20) * fakeOutline); // 作用：计算假描边强度。原理：视线越接近正脸且 mask 越高，描边越明显，pow(20) 让效果集中在特定角度。
        float2 outlineUV = float2(0, 0.0625); // 作用：指定一个固定 Ramp 采样坐标。原理：取色带最暗行附近作为描边颜色来源。
        float3 coolRampShadow = SAMPLE_TEXTURE2D(_BodyCoolRamp, sampler_BodyCoolRamp, outlineUV).rgb; // 作用：采样冷色 Ramp 的暗部颜色。原理：作为描边候选色。
        float3 warmRampShadow = SAMPLE_TEXTURE2D(_BodyWarmRamp, sampler_BodyWarmRamp, outlineUV).rgb; // 作用：采样暖色 Ramp 的暗部颜色。原理：另一套描边候选色。
        float3 ramp = lerp(coolRampShadow, warmRampShadow, 0.5); // 作用：把冷暖暗色各取一半混合。原理：得到一个中性暗色用于脸描边。
        fakeOutlineColor = pow(saturate(ramp), _OutlineGamma); // 作用：通过 gamma 曲线加强描边色。原理：pow 可压暗或强化颜色层次，使描边更像墨线。
    #endif // 作用：结束脸部假描边条件编译。原理：非脸部区域不使用。

    // Albedo // 作用：说明下面合成最终主体颜色。原理：把各个光照项按加法/乘法组合。
    float3 albedo = 0; // 作用：初始化最终颜色。原理：从 0 开始逐项累加。
    albedo += indirectLightColor; // 作用：加上间接光。原理：环境漫反射基础亮度。
    albedo += mianLightColor; // 作用：加上主光 toon 漫反射。原理：角色主要受光项。
    albedo += specularColor; // 作用：加上高光。原理：镜面反射增强材质表现。
    albedo *= stockingsEffect; // 作用：整体乘丝袜效果。原理：丝袜是对主体颜色的调制，不是独立加色。
    albedo += rimLightColor * lerp(1, albedo, _RimLightMixAlbedo); // 作用：加上边缘光，并可混入底色。原理：让 rim 可以纯加亮，也可以带一点材质本色。
    albedo += emissionColor; // 作用：加上自发光。原理：发光项通常直接累加。
    albedo = lerp(albedo, fakeOutlineColor, fakeOutlineEffect); // 作用：按强度把最终颜色往脸部假描边色推。原理：让指定区域出现轮廓线感。

    // Alpha // 作用：说明下面计算透明度。原理：目前主体 alpha 主要来自统一材质参数。
    float alpha = _Alpha; // 作用：初始化透明度。原理：直接用材质面板上的全局 alpha。

    // 避免背部看到眉毛 // 作用：说明下面是 overlay 模式下的特殊 alpha 修正。原理：当从背后看头部时，降低某些前脸叠加内容的可见性。
    #if _DRAW_OVERLAY_ON // 作用：只有开启 overlay 时才做这一步。原理：这个问题只在叠加绘制时出现。
        float3 headForward = normalize(_HeadForward); // 作用：归一化头部前向量。原理：用于和视线方向比较。
        alpha = lerp(1, alpha, saturate(dot(headForward, viewDirectionWS))); // 越小Alpha越接近1  // 作用：根据视角修正 alpha。原理：当视线更接近背面时 dot 更小，结果更偏向 1，从而避免背面透看到前脸细节。
    #endif // 作用：结束 overlay alpha 修正。原理：仅叠加 pass 需要。
    
    //Debug // 作用：说明下面是调试输出模式。原理：用 switch 切换不同中间结果到屏幕上观察。
    switch(_DebugColor)  // 作用：根据调试枚举选择输出内容。原理：运行时切换不同 shading 中间变量。
    {
        case 2: // 作用：调试模式 2 输出 baseColor。原理：检查底色贴图是否正确。
            albedo = baseColor.rgb; // 作用：把最终显示颜色替换成底色。原理：跳过后续光照结果便于调试纹理。
        break; // 作用：结束该 case。原理：防止贯穿到下一个分支。
        case 3: // 作用：调试模式 3 输出 indirectLightColor。原理：检查环境光/AO。
            albedo = indirectLightColor; // 作用：显示间接光结果。原理：直接观察球谐和 AO 的贡献。
        break; // 作用：结束该 case。原理：防止贯穿。
        case 4: // 作用：调试模式 4 输出主光颜色。原理：检查 toon 主光和 ramp。
            albedo = mianLightColor; // 作用：显示主光项。原理：独立观察主光 shading。
        break; // 作用：结束该 case。原理：防止贯穿。
        case 5: // 作用：调试模式 5 输出主光阴影值。原理：检查阴影阈值和脸部 SDF。
            albedo = mainLightShadow.rrr; // 作用：把单通道阴影值复制到 RGB。原理：以灰度图形式显示。
        break; // 作用：结束该 case。原理：防止贯穿。
        case 6: // 作用：调试模式 6 输出 rampColor。原理：检查冷暖色带混合结果。
            albedo = rampColor; // 作用：直接显示采样到的 Ramp 颜色。原理：便于查看图集行和冷暖插值。
        break; // 作用：结束该 case。原理：防止贯穿。
        case 7: // 作用：调试模式 7 输出高光。原理：检查 specular mask 和金属度逻辑。
            albedo = specularColor; // 作用：只显示高光项。原理：便于观察高光是否溢出或缺失。
        break; // 作用：结束该 case。原理：防止贯穿。
        default: // 作用：默认模式。原理：保持正常渲染结果。
            // albedo = albedo; // 作用：无实际执行，仅提示默认不改 albedo。原理：注释行不参与编译。
        break; // 作用：结束 default。原理：语法完整。
    }

    float4 color = float4(albedo, alpha); // 作用：把最终 RGB 和 alpha 组合成输出颜色。原理：片元着色器输出是 float4。
    clip(color.a - _AlphaClip); // 作用：做 alpha 裁剪。原理：当 alpha 小于阈值时丢弃该像素，实现硬裁切透明。
    color.rgb = MixFog(color.rgb, input.positionWSAndFogFactor.w); // 作用：把雾效混合进最终颜色。原理：按顶点传来的 fogFactor 与场景雾颜色插值。
    
    return color; // 作用：返回最终片元颜色。原理：写入当前渲染目标。
} // 作用：结束片元着色器。原理：函数作用域结束。
#endif // 作用：结束头文件保护宏。原理：与文件开头的 #ifndef / #define 配对。*