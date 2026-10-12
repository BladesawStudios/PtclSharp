#version 450 core

in vec2 uv;
layout(location = 0) out float scaled;

uniform sampler2D linearDepth;   // the scene's view-space distance, as texel * distanceScale + distanceOffset world units
uniform float distanceScale;
uniform float distanceOffset;
uniform float scale;             // the particle shaders' depth scale (view block row 18, w)

// The particle shaders compute the scene distance as texel * scale + offset, so the texture holds the distance divided by the scale.
void main()
{
    scaled = (texture(linearDepth, uv).r * distanceScale + distanceOffset) / scale;
}
