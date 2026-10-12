#version 450 core

in vec2 uv;
layout(location = 0) out vec4 color;

uniform sampler2D source;
uniform vec2 texel;          // one source texel
uniform float threshold;     // brightness where bloom starts
uniform float knee;          // width of the soft transition below the threshold

float brightness(vec3 c)
{
    return max(c.r, max(c.g, c.b));
}

// Soft threshold: 0 well below the knee, the full colour above the threshold.
vec3 prefilter(vec3 c)
{
    float b = brightness(c);
    float soft = clamp(b - threshold + knee, 0.0, 2.0 * knee);
    soft = soft * soft / (4.0 * knee + 1e-5);
    float weight = max(soft, b - threshold) / max(b, 1e-5);
    return c * weight;
}

// A 4x4 box of bilinear taps with each group weighted by its distance from the centre, which keeps single bright pixels from flickering.
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

    vec3 center = (d + e + i + j) * 0.25;
    vec3 groups[4] = vec3[4]((a + b + f + g) * 0.25, (b + c + g + h) * 0.25, (f + g + k + l) * 0.25, (g + h + l + m) * 0.25);
    vec3 sum = prefilter(center) * 0.5;
    for (int n = 0; n < 4; n++)
        sum += prefilter(groups[n]) * 0.125;
    color = vec4(sum, 1.0);
}
