const vertex_shader_source_2 = `#version 300 es

in vec2 aVertexPosition;
in vec2 aTexPosition;

out vec2 tex_pos;

uniform mat4 projection_matrix;
uniform mat4 model_matrix;

void main(void) 
{
    gl_Position = projection_matrix * model_matrix * vec4(aVertexPosition.xy, 0, 1);
    tex_pos = aTexPosition;
}
`;

//***************************************************************************** */

const fragment_shader_source_2 = `#version 300 es

precision mediump float;

uniform sampler2D logo_texture;

in vec2 tex_pos;
out vec4 fragColor;

//-----------------------------------------------------------------------------
//                                  MAIN
//-----------------------------------------------------------------------------

void main()
{
    fragColor = texture(logo_texture, tex_pos);
}
`;