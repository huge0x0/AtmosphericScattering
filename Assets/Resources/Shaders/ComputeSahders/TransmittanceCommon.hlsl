#if !defined(_TRANSMITTANCE_COMMON_HLSL)
#define _TRANSMITTANCE_COMMON_HLSL

#define _OpticalDepthScale 0.001
#define _OpticalDepthScaleInv 1000

//下面的参数变化以后会触发opticalPathLUT的更新
float _PlanetaryRadius;
float _ScaledHeightRayleigh;
float _ScaledHeightMie;
float _AtmosphericMaxHeight;

#define _TransmittanceEncodeMode_Simple 1
#define _TransmittanceEncodeMode_Distance 1

float Remap(float x, float min, float max){
    return (x - min) / (max - min);
}

float2 EncodeTransmittanceCoord(float height, float cosZenith){
    #if _TransmittanceEncodeMode_Simple == 1
        //简单的直接映射,这里v使用视线方向，对应为天顶角的cos值
        float u = saturate(height / _AtmosphericMaxHeight);
        float v = cosZenith * 0.5 + 0.5;
        return float2(u, v);
    #elif _TransmittanceEncodeMode_Distance == 1
        
    #endif
}

void DecodeTrtansmisionCoord(float2 uv, out float height, out float cosZenith){
    #if _TransmittanceEncodeMode_Simple == 1
        height = uv.x * _AtmosphericMaxHeight;
        cosZenith = uv.y * 2 - 1;
    #elif _TransmittanceEncodeMode_Distance == 1
        
    #endif
}

void CalculateRayEndLength(float op, float dirY, out float rayEndLength, out float dirY2)
{
    //使用余弦定理直接计算，p为视点，o为地球球心，a为射线与大气顶层交点
    const float oa = _PlanetaryRadius + _AtmosphericMaxHeight;
    const float op2 = op * op;
    dirY2 = dirY * dirY;
    rayEndLength = sqrt(op2 * dirY2 + oa * oa - op2) - op * dirY;
}

bool CalculateRayIntersectWithPlanetary(float3 beginPos, float3 rayDir, out float t)
{
    //这里用解析几何直接计算射线和球的相交
    //射线方程p = beginPos + t * rayDir
    //地球圆方程|p|²=r²，球心就是原点，r为地球半径
    //联立得t² + 2(beginPos * rayDir)t + |beginPos|² = r²
    float b = 2 * mul(beginPos, rayDir);
    float c = mul(beginPos, beginPos) - _PlanetaryRadius * _PlanetaryRadius;
    float delta = b * b - 4 * c;
    float hasIntersect = delta > 0;
    float sqrtDelta = sqrt(abs(delta));
    float t1 = (-b - sqrtDelta) * 0.5;
    float t2 = (-b + sqrtDelta) * 0.5;
    t = min(t1, t2);
    
    return hasIntersect && t > 0;
}

float CalculateVisibility(float centerToPointLen, float cosZenith, float sunVisibilityRange)
{
    float tangentSin = _PlanetaryRadius / centerToPointLen;
    float tangentCos = -sqrt(1 - tangentSin * tangentSin);
    float diff = cosZenith - (tangentCos - sunVisibilityRange * 0.5);
    return saturate(diff / sunVisibilityRange);
}

float GetAtmosphericDensity(float height, float scaleHeight){
    return exp(-height / scaleHeight);
}



#endif //_TRANSMITTANCE_COMMON_HLSL