#version 450
layout(location = 0) in vec2 inPosition;
layout(location = 1) in vec4 inColor;
layout(location = 2) in vec2 instancePos;
layout(location = 3) in vec4 instanceCol;

layout(location = 0) out vec4 fragColor;

void main() {
    gl_Position = vec4(inPosition+instancePos, 0.0, 1.0);
    fragColor = instanceCol;
}