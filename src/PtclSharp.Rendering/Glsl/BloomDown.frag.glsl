#version 450 core

in vec2 uv;
layout(location = 0) out vec4 color;

uniform sampler2D source;
uniform vec2 texel;          // one source texel

// The same 13-tap filter as the prefilter, without the threshold.
void main()
{
    vec3 a = texture(source, uv + texel * vec2(-2.0, -2.0)).rgb;
    vec3 b = texture(source, uv + texel * vec2( 0.0, -2.0)).rgb;
    vec3 c = texture(source, uv + texel * vec2( 2.0, -2.0)).rgb;
    vec3 d = texture(source, uv + texel * vec2(-1.0, -1.0)).rgb;
    vec3 e = texture(source, uv + texel * vec2( 1.0, -1.0)).rgb;
    vec3 f = texture(source, uv + texel * vec2(-2.0,  0.0)).rgb;
    vec3 g = texture(source, uv).rgb;
    vec3 h = texture(source, uv + texel * vec2( 2.0,  0.0)).rgb;
    vec3 i = texture(source, uv + texel * vec2(-1.0,  1.0)).rgb;
    vec3 j = texture(source, uv + texel * vec2( 1.0,  1.0)).rgb;
    vec3 k = texture(source, uv + texel * vec2(-2.0,  2.0)).rgb;
    vec3 l = texture(source, uv + texel * vec2( 0.0,  2.0)).rgb;
    vec3 m = texture(source, uv + texel * vec2( 2.0,  2.0)).rgb;

    vec3 sum = (d + e + i + j) * 0.125;
    sum += (a + b + f + g) * 0.03125;
    sum += (b + c + g + h) * 0.03125;
    sum += (f + g + k + l) * 0.03125;
    sum += (g + h + l + m) * 0.03125;
    color = vec4(sum, 1.0);
}
