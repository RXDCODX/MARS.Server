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

uniform vec2 iResolution;
uniform vec2 texture_size;
uniform sampler2D border_texture;
uniform sampler2D corner_texture;

in vec2 tex_pos;
out vec4 fragColor;

//-----------------------------------------------------------------------------

vec4 get_texture_color(vec2 input_coord, sampler2D in_texture, vec2 texture_size)
{
    // приводим координаты p к диапазону текстуры
    vec2 tex_coords = input_coord / texture_size;
    
    // сэмплируем текстуру шума и возвращаем значение
    return texture(in_texture, tex_coords);
}

//-----------------------------------------------------------------------------
//                                  MAIN
//-----------------------------------------------------------------------------

void main()
{
    vec2 input_coords = tex_pos * iResolution.xy;

    vec4 border_color = get_texture_color(input_coords, border_texture, texture_size);
    vec4 arc_color = get_texture_color(input_coords, corner_texture, texture_size);

    fragColor = max(border_color, arc_color);
}
`;