#version 450 core

in vec2 uv;
layout(location = 0) out vec4 color;

uniform sampler2D scene;
uniform sampler2D bloom;
uniform float exposure;
uniform float bloomIntensity;   // 0 skips the bloom lookup's contribution
uniform float flipVertical;     // 1 when the scene was drawn with the window origin at the top

vec3 aces(vec3 x)
{
    return clamp((x * (2.51 * x + 0.03)) / (x * (2.43 * x + 0.59) + 0.14), 0.0, 1.0);
}

void main()
{
    vec2 coordinate = vec2(uv.x, mix(uv.y, 1.0 - uv.y, flipVertical));
    vec3 hdr = texture(scene, coordinate).rgb + texture(bloom, coordinate).rgb * bloomIntensity;
    color = vec4(pow(aces(hdr * exposure), vec3(1.0 / 2.2)), 1.0);
}
