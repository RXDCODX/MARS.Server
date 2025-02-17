function init_WebGL(canvas) 
{
    const gl = canvas.getContext('webgl2');
    if (!gl) 
    {
        alert('Unable to initialize WebGL2. Your browser may not support it.');
        return null;
    }

    return gl;
}

//*****************************************************************************

function normalize(value, min, max) 
{
    return (value - min) / (max - min);
}

//*****************************************************************************

function to_radians(angle)
{
	return angle * Math.PI / 180.0;
}

//*****************************************************************************

function to_seconds(time)
{
    return time * 0.001;
}

//*****************************************************************************

function to_millisecond(time)
{
    return time * 1000;
}

//*****************************************************************************

function is_number(value)
{
    return typeof value === 'number';
}

//*****************************************************************************

function is_integer(value)
{
    return is_number(value) && Number.isInteger(value);
}

//*****************************************************************************

function is_float(value)
{
    return is_number(value) && !Number.isInteger(value);
}

//*****************************************************************************

function is_boolean(value)
{
    return typeof value === 'boolean';
}

//*****************************************************************************

function is_array(value)
{
    return Array.isArray(value) || ArrayBuffer.isView(value);
}

//*****************************************************************************

function is_uint8array(value)
{
    return is_array(value) && value instanceof Uint8Array;
}

//*****************************************************************************

function is_vector(value, elements_count)
{
    return is_array(value) && value.length === elements_count;
}

//*****************************************************************************

function is_image(value)
{
    return value instanceof Image;
}

//*****************************************************************************

function is_matrix(value, rows, cols)
{
    return is_array(value) && value.length === (rows * cols);
}

//*****************************************************************************

function is_string(value)
{
    return typeof value === 'string';
}

//*****************************************************************************

function clamp(value, min, max)
{
    return Math.min(Math.max(value, min), max);
}

//*****************************************************************************

function is_valide_texture(gl_context, texture)
{
    return texture && texture.get_native_handle() && 
        gl_context.isTexture(texture.get_native_handle());
}

//*****************************************************************************

function create_texture(gl, width, height) 
{
    const texture = gl.createTexture();
    gl.bindTexture(gl.TEXTURE_2D, texture);
    gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA, width, height, 0, gl.RGBA, 
        gl.UNSIGNED_BYTE, null);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.LINEAR);
	gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.LINEAR);

    return new Texture(texture, width, height, width * height * 4);
}

//*****************************************************************************

function delete_texture(gl_context, texture)
{
    gl_context.deleteTexture(texture.get_native_handle());
}

//*****************************************************************************

function bind_texture(gl_context, texture)
{
    if(!texture) return;

    gl_context.bindTexture(gl_context.TEXTURE_2D, texture.get_native_handle());
}

//*****************************************************************************

function bind_texture_unit(gl_context, texture, unit_index = 0)
{
    if(!texture || unit_index < 0) return;

    const texture_unit = gl_context.TEXTURE0 + unit_index;

    gl_context.activeTexture(texture_unit);
    bind_texture(gl_context, texture);
}

//*****************************************************************************

function create_framebuffer(gl, texture) 
{
    const framebuffer = gl.createFramebuffer();
    gl.bindFramebuffer(gl.FRAMEBUFFER, framebuffer);
    gl.framebufferTexture2D(gl.FRAMEBUFFER, gl.COLOR_ATTACHMENT0, 
        gl.TEXTURE_2D, texture, 0);

    return framebuffer;
}

//*****************************************************************************

function create_orthographic_matrix(left, right, bottom, top, near, far) 
{
    let projection_matrix = mat4.create();
	
    mat4.ortho(projection_matrix, left, right, bottom, top, near, far);
	
    return projection_matrix;
}

//*****************************************************************************

function is_intersect_ray_segment(ray_start, ray_direction, start_line, 
    end_line) 
{
    let A = ray_start;
    let d = ray_direction;
    let B = start_line;
    let C = end_line;

    // Направление отрезка
    let e = [C[0] - B[0], C[1] - B[1]];
    
    // Составляем систему уравнений
    let det = d[0] * e[1] - d[1] * e[0];

    if (det === 0) 
    {
        return null; // Луч параллелен отрезку или вырожденный случай
    }
    
    let t = ((B[0] - A[0]) * e[1] - (B[1] - A[1]) * e[0]) / det;
    let u = ((B[0] - A[0]) * d[1] - (B[1] - A[1]) * d[0]) / det;
    
    if (t >= 0 && u >= 0 && u <= 1) 
    {
        // Находим точку пересечения
        let intersectionX = A[0] + t * d[0];
        let intersectionY = A[1] + t * d[1];
        return [intersectionX, intersectionY];
    } 
    else 
    {
        return null; // Нет пересечения или пересечение за пределами отрезка
    }
}

//*****************************************************************************

function create_arc(center, radius, segments_count, start_angle = 0, 
    end_angle = Math.PI * 2)
{
    const TAU = Math.PI * 2;
    const angle = Math.min(TAU, end_angle - start_angle);

    const positions = [];
    const tex_coords = [];

    if(angle == TAU)
    {
        positions.push(center[0], center[1]);
        tex_coords.push(0.0, 0.0);
    }

    const segments = Math.round(segments_count * angle / TAU);

    let x0 = center[0];
    let y0 = center[1];

    for(let i = 0; i <= segments; i++) 
    {
        const i_angle = start_angle + angle * i / segments;

        const cos_angle = Math.cos(i_angle);
        const sin_angle = Math.sin(i_angle);

        const x1 = x0 + radius * cos_angle;
        const y1 = y0 + radius * sin_angle;

        positions.push(x1, y1);

        // Нормализуем угол в диапазон от 0 до 1
        const norm_angle = (i_angle - start_angle) / angle;

        const tx_1 = norm_angle;
        const ty_1 = 1.0;

        tex_coords.push(tx_1, ty_1);
    }

    return { 'positions': positions, 'tex_coords': tex_coords };
}

//*****************************************************************************

function pack_vertices(gl, type, ...objects)
{
    let vertices = [];
    let res_tex_coords = [];

    for (const obj of objects) 
    {
        const model = obj.model.get_transform();
        const positions = obj.get_positions();
        const tex_coords = obj.get_tex_coords();
        const vertices_count = obj.get_vertices_count();

        for(let i = 0; i < vertices_count; i += 2)
        {
            let pos = vec2.fromValues(positions[i], positions[i + 1]);

            vec2.transformMat4(pos, pos, model);

            vertices.push(pos[0]);
            vertices.push(pos[1]);

            res_tex_coords.push(tex_coords[i]);
            res_tex_coords.push(tex_coords[i + 1]);
        }
    }

    return new Render_vertex_pack(gl, vertices, res_tex_coords, 
        vertices.length, type); 
}

//*****************************************************************************

function update_vertex_pack(pack, ...objects)
{
    let start_index = 0;

    let vertices = [];
    let res_tex_coords = [];
    
    for (const obj of objects) 
    {
        const model = obj.model.get_transform();
        const positions = obj.get_positions();
        const tex_coords = obj.get_tex_coords();
        const vertices_count = obj.get_vertices_count();

        for(let i = 0; i < vertices_count; i += 2)
        {
            let pos = vec2.fromValues(positions[i], positions[i + 1]);

            vec2.transformMat4(pos, pos, model);

            vertices.push(pos[0]);
            vertices.push(pos[1]);

            res_tex_coords.push(tex_coords[i]);
            res_tex_coords.push(tex_coords[i + 1]);
        }
    }

    pack.update(start_index, vertices.length, vertices, res_tex_coords);
}

//*****************************************************************************

function log_print(...args)
{
    if(!shader_settings.is_debug_version) return;

    console.log(...args);
}

//*****************************************************************************

function error_print(...args)
{
    console.error(...args);
}

//*****************************************************************************