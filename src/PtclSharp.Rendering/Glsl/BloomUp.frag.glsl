#version 450 core

in vec2 uv;
layout(location = 0) out vec4 color;

uniform sampler2D source;    // the smaller level
uniform vec2 texel;          // one source texel
uniform float radius;        // tent filter spread, in source texels

// A 3x3 tent filter; the result is blended additively onto the larger level.
void main()
{
    vec2 r = texel * radius;
    vec3 sum = texture(source, uv + vec2(-r.x, -r.y)).rgb
             + texture(source, uv + vec2( 0.0, -r.y)).rgb * 2.0
             + texture(source, uv + vec2( r.x, -r.y)).rgb
             + texture(source, uv + vec2(-r.x,  0.0)).rgb * 2.0
             + texture(source, uv).rgb * 4.0
             + texture(source, uv + vec2( r.x,  0.0)).rgb * 2.0
             + texture(source, uv + vec2(-r.x,  r.y)).rgb
             + texture(source, uv + vec2( 0.0,  r.y)).rgb * 2.0
             + texture(source, uv + vec2( r.x,  r.y)).rgb;
    color = vec4(sum * (1.0 / 16.0), 1.0);
}
