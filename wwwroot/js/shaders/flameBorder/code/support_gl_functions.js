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

function create_shader(gl, source_code, type) 
{
    const shader = gl.createShader(type);
    gl.shaderSource(shader, source_code);
    gl.compileShader(shader);

    if (!gl.getShaderParameter(shader, gl.COMPILE_STATUS)) 
    {
        console.error('An error occurred compiling the shaders: ' + 
			gl.getShaderInfoLog(shader));
        gl.deleteShader(shader);

        return null;
    }

    return shader;
}

//*****************************************************************************

function create_program(gl, vertex_shader, fragment_shader) 
{
    const shader_program = gl.createProgram();
    gl.attachShader(shader_program, vertex_shader);
    gl.attachShader(shader_program, fragment_shader);
    gl.linkProgram(shader_program);
    if (!gl.getProgramParameter(shader_program, gl.LINK_STATUS)) 
    {
        console.error('Unable to initialize the shader program: ' + 
			gl.getProgramInfoLog(shaderProgram));

        return null;
    }

    return shader_program;
}

//*****************************************************************************

function normalize(value, min, max) 
{
    return (value - min) / (max - min);
}

//*****************************************************************************

function get_uniform_location(gl, shader, name)
{
    return gl.getUniformLocation(shader, name);
}

//*****************************************************************************

function to_radians(angle)
{
	return angle * Math.PI / 180.0;
}

//*****************************************************************************

function create_ring(center, inner_radius, outer_radius, segments_count = 360, 
    start_angle = 0, end_angle = Math.PI * 2) 
{
    const TAU = Math.PI * 2;
    const angle = Math.min(TAU, end_angle - start_angle);

    const positions = angle === TAU ? [] : [center[0], center[1]];
    const tex_coords = angle === TAU ? [] : [center[0], center[1]];

    const segments = Math.round(segments_count * angle / TAU);

    const x0 = center[0];
    const y0 = center[1];

    for(let i = 0; i <= segments; i++) 
    {
        const i_angle = start_angle + angle * i / segments;

        const cos_angle = Math.cos(i_angle);
        const sin_angle = Math.sin(i_angle);

        const x1 = x0 + inner_radius * cos_angle;
        const y1 = y0 + inner_radius * sin_angle;
        const x2 = x0 + outer_radius * cos_angle;
        const y2 = y0 + outer_radius * sin_angle;

        positions.push(x1, y1, x2, y2);

        // Нормализуем угол в диапазон от 0 до 1
        const norm_angle = (i_angle - start_angle) / angle;

        // Для внутреннего радиуса
        const tx_1 = norm_angle;
        const ty_1 = 1.0;

        // Для внешнего радиуса
        const tx_2 = norm_angle;
        const ty_2 = 0.0;

        tex_coords.push(tx_1, ty_1, tx_2, ty_2);
    }

    let result = new Ring(inner_radius, outer_radius, segments_count, 
        start_angle, end_angle);
    result.positions = new Float32Array(positions);
    result.tex_coords = new Float32Array(tex_coords);
    result.vertices_count = positions.length;

    return result;

    //return { 'positions': positions, 'tex_coords': tex_coords };
}

//*****************************************************************************

function create_sprite(width, height)
{
    const positions = [
        0, 0,
        0, 0 + height,
        0 + width, 0,
        0 + width, 0 + height,
    ];

    const tex_coords = [
        0.0, 0.0,
        0.0, 1.0,
        1.0, 0.0,
        1.0, 1.0,
    ];

    let result = new Sprite(width, height);
    result.positions = new Float32Array(positions);
    result.tex_coords = new Float32Array(tex_coords);
    result.vertices_count = positions.length;
    
    return result;
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

function create_texture(gl, width, height) 
{
    const texture = gl.createTexture();
    gl.bindTexture(gl.TEXTURE_2D, texture);
    gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA, width, height, 0, gl.RGBA, 
        gl.UNSIGNED_BYTE, null);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.LINEAR);
	gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.LINEAR);

    return texture;
}

//*****************************************************************************

function rotate_z(point, center, angle) 
{
    // Переводим угол из градусов в радианы
    var angle_rad = angle * Math.PI / 180.0;

    // Разбиваем точки на составляющие
    var x1 = point.x;
    var y1 = point.y;
    var x2 = center.x;
    var y2 = center.y;

    // Вычисляем новые координаты точки после поворота
    var new_x = (x1 - x2) * Math.cos(angle_rad) - (y1 - y2) * Math.sin(angle_rad) + x2;
    var new_y = (x1 - x2) * Math.sin(angle_rad) + (y1 - y2) * Math.cos(angle_rad) + y2;

    // Возвращаем новые координаты точки
    return [new_x, new_y];
}

//*****************************************************************************

function is_intersect_ray_segment(ray_start, ray_direction, start_line, end_line) 
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

function create_circle(center, radius, segments_count = 60)
{
    let circle_data = create_arc(center, radius, segments_count,
        to_radians(0.0), to_radians(360.0));

    let result = new Circle(radius);
    result.positions = new Float32Array(circle_data['positions']);
    result.tex_coords = new Float32Array(circle_data['tex_coords']);
    result.vertices_count = circle_data['positions'].length;

    return result;
}

//*****************************************************************************

function create_rectangle_with_hole(width, height, radius, segments = 60,
    start_angle = 0, end_angle = 360)
{
    let min_angle = Math.min(start_angle, end_angle);
    let max_angle = Math.max(start_angle, end_angle);

    start_angle = min_angle;
    end_angle = max_angle;  

    let circle_center = [width / 2.0, height / 2.0];
    let hole_positions = create_arc(circle_center, radius, segments, 
        start_angle, end_angle);
    
    let ld_rect = [0.0, 0.0];
    let lu_rect = [0.0, height];
    let ru_rect = [width, height];
    let rd_rect = [width, 0.0];

    let m_ld_rect = [circle_center[0] - radius, circle_center[1] - radius];
    let m_lu_rect = [circle_center[0] - radius, circle_center[1] + radius];
    let m_ru_rect = [circle_center[0] + radius, circle_center[1] + radius];
    let m_rd_rect = [circle_center[0] + radius, circle_center[1] - radius];


    let down_vertices = [];
    let down_tex_coords = [];

    let middle_vertices = [];
    let middle_tex_coords = [];

    for(let i = 0; i < hole_positions['positions'].length; i += 2)
    {
        let circle_pos = [
            hole_positions['positions'][i], 
            hole_positions['positions'][i + 1]];

        let direction = [
            circle_pos[0] - circle_center[0], 
            circle_pos[1] - circle_center[1]];

        direction[0] *= width * 10;
        direction[1] *= height * 10;

        let up_pos = is_intersect_ray_segment(circle_center, direction, 
            lu_rect, ru_rect);
        let right_pos = is_intersect_ray_segment(circle_center, direction, 
            ru_rect, rd_rect);
        let down_pos = is_intersect_ray_segment(circle_center, direction, 
            ld_rect, rd_rect);
        let left_pos = is_intersect_ray_segment(circle_center, direction, 
            lu_rect, ld_rect);

        let m_up_pos = is_intersect_ray_segment(circle_center, direction, 
            m_lu_rect, m_ru_rect);
        let m_right_pos = is_intersect_ray_segment(circle_center, direction, 
            m_ru_rect, m_rd_rect);
        let m_down_pos = is_intersect_ray_segment(circle_center, direction, 
            m_ld_rect, m_rd_rect);
        let m_left_pos = is_intersect_ray_segment(circle_center, direction, 
            m_lu_rect, m_ld_rect);

        if(up_pos && m_up_pos)
        {
            down_vertices.push(up_pos[0], up_pos[1]);
            middle_vertices.push(m_up_pos[0], m_up_pos[1]);
            down_tex_coords.push(hole_positions['tex_coords'][i], 0.0);
            middle_tex_coords.push(hole_positions['tex_coords'][i], 0.9);
        }

        if(right_pos && m_right_pos)
        {
            down_vertices.push(right_pos[0], right_pos[1]);
            middle_vertices.push(m_right_pos[0], m_right_pos[1])
            down_tex_coords.push(hole_positions['tex_coords'][i], 0.0);
            middle_tex_coords.push(hole_positions['tex_coords'][i], 0.9);
        }

        if(down_pos && m_down_pos)
        {
            down_vertices.push(down_pos[0], down_pos[1]);
            middle_vertices.push(m_down_pos[0], m_down_pos[1]);
            down_tex_coords.push(hole_positions['tex_coords'][i], 0.0);
            middle_tex_coords.push(hole_positions['tex_coords'][i], 0.9);
        }

        if(left_pos && m_left_pos)
        {
            down_vertices.push(left_pos[0], left_pos[1]);
            middle_vertices.push(m_left_pos[0], m_left_pos[1]);
            down_tex_coords.push(hole_positions['tex_coords'][i], 0.0);
            middle_tex_coords.push(hole_positions['tex_coords'][i], 0.9);
        }
    }

    let rect_vertices = [];
    let rect_tex_coords = [];

    for(let i = 0; i < down_vertices.length / 2; i++)
    {
        let idx = 2 * i;    
        let idx_1 = 2 * i + 1;
        let idx_2 = 2 * i + 2;
        let idx_3 = 2 * i + 3;

        let lu_pos = [
            hole_positions['positions'][idx], 
            hole_positions['positions'][idx_1]];
        let ru_pos = [
            hole_positions['positions'][idx_2], 
            hole_positions['positions'][idx_3]];
        let ld_pos = [
            middle_vertices[idx], 
            middle_vertices[idx_1]];
        let rd_pos = [
            middle_vertices[idx_2], 
            middle_vertices[idx_3]];

        let lu_tex_pos = [
            hole_positions['tex_coords'][idx], 
            hole_positions['tex_coords'][idx_1]];
        let ru_tex_pos = [
            hole_positions['tex_coords'][idx_2], 
            hole_positions['tex_coords'][idx_3]];
        let ld_tex_pos = [
            middle_tex_coords[idx], 
            middle_tex_coords[idx_1]];
        let rd_tex_pos = [
            middle_tex_coords[idx_2], 
            middle_tex_coords[idx_3]];

        rect_vertices.push(
            lu_pos[0], lu_pos[1], 
            ru_pos[0], ru_pos[1], 
            rd_pos[0], rd_pos[1],

            rd_pos[0], rd_pos[1], 
            ld_pos[0], ld_pos[1], 
            lu_pos[0], lu_pos[1]);

        rect_tex_coords.push(
            lu_tex_pos[0], lu_tex_pos[1], 
            ru_tex_pos[0], ru_tex_pos[1], 
            rd_tex_pos[0], rd_tex_pos[1],

            rd_tex_pos[0], rd_tex_pos[1], 
            ld_tex_pos[0], ld_tex_pos[1], 
            lu_tex_pos[0], lu_tex_pos[1]);
    }

    for(let i = 0; i < down_vertices.length / 2; i++)
    {
        let idx = 2 * i;
        let idx_1 = 2 * i + 1;
        let idx_2 = 2 * i + 2;
        let idx_3 = 2 * i + 3;

        let lu_pos = [middle_vertices[idx], middle_vertices[idx_1]];
        let ru_pos = [middle_vertices[idx_2], middle_vertices[idx_3]];
        let ld_pos = [down_vertices[idx], down_vertices[idx_1]];
        let rd_pos = [down_vertices[idx_2], down_vertices[idx_3]];

        let lu_tex_pos = [middle_tex_coords[idx], middle_tex_coords[idx_1]];
        let ru_tex_pos = [middle_tex_coords[idx_2], middle_tex_coords[idx_3]];
        let ld_tex_pos = [down_tex_coords[idx], down_tex_coords[idx_1]];
        let rd_tex_pos = [down_tex_coords[idx_2], down_tex_coords[idx_3]];

        rect_vertices.push(
            lu_pos[0], lu_pos[1], 
            ru_pos[0], ru_pos[1], 
            rd_pos[0], rd_pos[1],

            rd_pos[0], rd_pos[1], 
            ld_pos[0], ld_pos[1], 
            lu_pos[0], lu_pos[1]);

        rect_tex_coords.push(
            lu_tex_pos[0], lu_tex_pos[1], 
            ru_tex_pos[0], ru_tex_pos[1], 
            rd_tex_pos[0], rd_tex_pos[1],

            rd_tex_pos[0], rd_tex_pos[1], 
            ld_tex_pos[0], ld_tex_pos[1], 
            lu_tex_pos[0], lu_tex_pos[1]);
    }

    let result = new Rect_with_hole(width, height, radius);
    result.positions = new Float32Array(rect_vertices);
    result.tex_coords = new Float32Array(rect_tex_coords);
    result.vertices_count = rect_vertices.length;

    return result;
}

//*****************************************************************************

function create_corner(width, height, radius, segments = 60,
    start_angle = 0, end_angle = 360)
{
    let min_angle = Math.min(start_angle, end_angle);
    let max_angle = Math.max(start_angle, end_angle);

    start_angle = min_angle;
    end_angle = max_angle;  

    let circle_center = [width / 2.0, height / 2.0];
    let hole_positions = create_arc(circle_center, radius, segments, 
        start_angle, end_angle);
    
    let ld_rect = [0.0, 0.0];
    let lu_rect = [0.0, height];
    let ru_rect = [width, height];
    let rd_rect = [width, 0.0];

    let m_ld_rect = [circle_center[0] - radius, circle_center[1] - radius];
    let m_lu_rect = [circle_center[0] - radius, circle_center[1] + radius];
    let m_ru_rect = [circle_center[0] + radius, circle_center[1] + radius];
    let m_rd_rect = [circle_center[0] + radius, circle_center[1] - radius];


    let down_vertices = [];
    let down_tex_coords = [];

    let middle_vertices = [];
    let middle_tex_coords = [];

    for(let i = 0; i < hole_positions['positions'].length; i += 2)
    {
        let circle_pos = [
            hole_positions['positions'][i], 
            hole_positions['positions'][i + 1]];

        let direction = [
            circle_pos[0] - circle_center[0], 
            circle_pos[1] - circle_center[1]];

        direction[0] *= width * 10;
        direction[1] *= height * 10;

        let up_pos = is_intersect_ray_segment(circle_center, direction, 
            lu_rect, ru_rect);
        let right_pos = is_intersect_ray_segment(circle_center, direction, 
            ru_rect, rd_rect);
        let down_pos = is_intersect_ray_segment(circle_center, direction, 
            ld_rect, rd_rect);
        let left_pos = is_intersect_ray_segment(circle_center, direction, 
            lu_rect, ld_rect);

        let m_up_pos = is_intersect_ray_segment(circle_center, direction, 
            m_lu_rect, m_ru_rect);
        let m_right_pos = is_intersect_ray_segment(circle_center, direction, 
            m_ru_rect, m_rd_rect);
        let m_down_pos = is_intersect_ray_segment(circle_center, direction, 
            m_ld_rect, m_rd_rect);
        let m_left_pos = is_intersect_ray_segment(circle_center, direction, 
            m_lu_rect, m_ld_rect);

        if(up_pos && m_up_pos)
        {
            down_vertices.push(up_pos[0], up_pos[1]);
            middle_vertices.push(m_up_pos[0], m_up_pos[1]);
            down_tex_coords.push(hole_positions['tex_coords'][i], 0.0);
            middle_tex_coords.push(hole_positions['tex_coords'][i], 0.9);
        }

        if(right_pos && m_right_pos)
        {
            down_vertices.push(right_pos[0], right_pos[1]);
            middle_vertices.push(m_right_pos[0], m_right_pos[1])
            down_tex_coords.push(hole_positions['tex_coords'][i], 0.0);
            middle_tex_coords.push(hole_positions['tex_coords'][i], 0.9);
        }

        if(down_pos && m_down_pos)
        {
            down_vertices.push(down_pos[0], down_pos[1]);
            middle_vertices.push(m_down_pos[0], m_down_pos[1]);
            down_tex_coords.push(hole_positions['tex_coords'][i], 0.0);
            middle_tex_coords.push(hole_positions['tex_coords'][i], 0.9);
        }

        if(left_pos && m_left_pos)
        {
            down_vertices.push(left_pos[0], left_pos[1]);
            middle_vertices.push(m_left_pos[0], m_left_pos[1]);
            down_tex_coords.push(hole_positions['tex_coords'][i], 0.0);
            middle_tex_coords.push(hole_positions['tex_coords'][i], 0.9);
        }
    }

    return { 
        'top_position': hole_positions['positions'], 
        'middle_position': middle_vertices,
        'bottom_position': down_vertices,
        'top_tex_coords': hole_positions['tex_coords'],
        'middle_tex_coords': middle_tex_coords,
        'bottom_tex_coords': down_tex_coords
    };
}

//*****************************************************************************

function create_direction_sprite(pos_x, pos_y, width, height, segments, 
    direction)
{
    let top_position = [];
    let middle_position = [];
    let bottom_position = [];

    let top_tex_coords = [];
    let middle_tex_coords = [];
    let bottom_tex_coords = [];

    top_tex_coords.push(
        0.0, 1.0,
        0.5, 1.0,
        1.0, 1.0
    );

    middle_tex_coords.push(
        0.0, 0.5,
        0.5, 0.5,
        1.0, 0.5
    );

    bottom_tex_coords.push(
        0.0, 0.0,
        0.5, 0.0,
        1.0, 0.0
    );

    let dx = width / segments;
    let dy = height / segments;

    let tdx = 1.0 / segments;
    let tdy = 1.0 / segments;

    if(direction == 'top')
    {
        let start_x = pos_x;
        let start_tx = 0.0;
        let top_y = pos_y;
        let middle_y = pos_y + height / 2.0;
        let bottom_y = pos_y + height;

        for(let i = 0; i < segments + 1; i++)
        {
            top_position.push(start_x, top_y);
            middle_position.push(start_x, middle_y);
            bottom_position.push(start_x, bottom_y);

            top_tex_coords.push(start_tx, 1.0);
            middle_tex_coords.push(start_tx, 0.5);
            bottom_tex_coords.push(start_tx, 0.0);

            start_x += dx;
            start_tx += tdx;
        }
    }
    else if (direction == 'right')
    {
        let start_y = pos_y;
        let start_tx = 0.0;
        let top_x = pos_x;
        let middle_x = pos_x + width / 2.0;
        let bottom_x = pos_x + width;

        for(let i = 0; i < segments + 1; i++)
        {
            top_position.push(top_x, start_y);
            middle_position.push(middle_x, start_y);
            bottom_position.push(bottom_x, start_y);

            top_tex_coords.push(start_tx, 1.0);
            middle_tex_coords.push(start_tx, 0.5);
            bottom_tex_coords.push(start_tx, 0.0);

            start_y += dy;
            start_tx += tdy;
        }
    }
    else if (direction == 'bottom')
    {
        let start_x = pos_x;
        let start_tx = 0.0;
        let top_y = pos_y + height;
        let middle_y = pos_y + height / 2.0;
        let bottom_y = 0.0

        for(let i = 0; i < segments + 1; i++)
        {
            top_position.push(start_x, top_y);
            middle_position.push(start_x, middle_y);
            bottom_position.push(start_x, bottom_y);

            top_tex_coords.push(start_tx, 1.0);
            middle_tex_coords.push(start_tx, 0.5);
            bottom_tex_coords.push(start_tx, 0.0);

            start_x += dx;
            start_tx += tdx;
        }
    }
    else if (direction == 'left')
    {
        let start_y = pos_y;
        let start_tx = 0.0;
        let top_x = pos_x + width;
        let middle_x = pos_x + width / 2.0;
        let bottom_x = pos_x;

        for(let i = 0; i < segments + 1; i++)
        {
            top_position.push(top_x, start_y);
            middle_position.push(middle_x, start_y);
            bottom_position.push(bottom_x, start_y);

            top_tex_coords.push(start_tx, 1.0);
            middle_tex_coords.push(start_tx, 0.5);
            bottom_tex_coords.push(start_tx, 0.0);

            start_y += dy;
            start_tx += tdy;
        }
    }

    return { 
        'top_position': top_position, 
        'middle_position': middle_position,
        'bottom_position': bottom_position,
        'top_tex_coords': top_tex_coords,
        'middle_tex_coords': middle_tex_coords,
        'bottom_tex_coords': bottom_tex_coords
    };
}

//*****************************************************************************

function update_vertices(out_positions, out_tex_positions, in_array)
{
    console.log('UPD TOP: ', in_array['top_position'].length);
    console.log('UPD MID: ', in_array['middle_position'].length);
    console.log('UPD BOT: ', in_array['bottom_position'].length);

    for(let i = 0; i < in_array['bottom_position'].length / 2; i++)
    {
        let idx = 2 * i;    
        let idx_1 = 2 * i + 1;
        let idx_2 = 2 * i + 2;
        let idx_3 = 2 * i + 3;

        let top_pos = [
            in_array['top_position'][idx], 
            in_array['top_position'][idx_1], 
            in_array['top_position'][idx_2], 
            in_array['top_position'][idx_3]];
        let mid_pos = [
            in_array['middle_position'][idx], 
            in_array['middle_position'][idx_1], 
            in_array['middle_position'][idx_2], 
            in_array['middle_position'][idx_3]];
        let bot_pos = [
            in_array['bottom_position'][idx], 
            in_array['bottom_position'][idx_1], 
            in_array['bottom_position'][idx_2], 
            in_array['bottom_position'][idx_3]];

        let top_tex_pos = [
            in_array['top_tex_coords'][idx], 
            in_array['top_tex_coords'][idx_1], 
            in_array['top_tex_coords'][idx_2], 
            in_array['top_tex_coords'][idx_3]];
        let mid_tex_pos = [
            in_array['middle_tex_coords'][idx], 
            in_array['middle_tex_coords'][idx_1], 
            in_array['middle_tex_coords'][idx_2], 
            in_array['middle_tex_coords'][idx_3]];
        let bot_tex_pos = [
            in_array['bottom_tex_coords'][idx], 
            in_array['bottom_tex_coords'][idx_1], 
            in_array['bottom_tex_coords'][idx_2], 
            in_array['bottom_tex_coords'][idx_3]];

        let lu_pos = [top_pos[0], top_pos[1]];
        let ru_pos = [top_pos[2], top_pos[3]];
        let lm_pos = [mid_pos[0], mid_pos[1]];
        let rm_pos = [mid_pos[2], mid_pos[3]];
        let ld_pos = [bot_pos[0], bot_pos[1]];
        let rd_pos = [bot_pos[2], bot_pos[3]];

        let lu_tex_pos = [top_tex_pos[0], top_tex_pos[1]];
        let ru_tex_pos = [top_tex_pos[2], top_tex_pos[3]];
        let lm_tex_pos = [mid_tex_pos[0], mid_tex_pos[1]];
        let rm_tex_pos = [mid_tex_pos[2], mid_tex_pos[3]];
        let ld_tex_pos = [bot_tex_pos[0], bot_tex_pos[1]];
        let rd_tex_pos = [bot_tex_pos[2], bot_tex_pos[3]];

        out_positions.push(
            lu_pos[0], lu_pos[1], 
            ru_pos[0], ru_pos[1], 
            rm_pos[0], rm_pos[1],

            rm_pos[0], rm_pos[1], 
            lm_pos[0], lm_pos[1], 
            lu_pos[0], lu_pos[1]);

        out_tex_positions.push(
            lu_tex_pos[0], lu_tex_pos[1], 
            ru_tex_pos[0], ru_tex_pos[1], 
            rm_tex_pos[0], rm_tex_pos[1],

            rm_tex_pos[0], rm_tex_pos[1], 
            lm_tex_pos[0], lm_tex_pos[1], 
            lu_tex_pos[0], lu_tex_pos[1]);


        out_positions.push(
            lm_pos[0], lm_pos[1], 
            rm_pos[0], rm_pos[1], 
            rd_pos[0], rd_pos[1],

            rd_pos[0], rd_pos[1], 
            ld_pos[0], ld_pos[1], 
            lm_pos[0], lm_pos[1]);

        out_tex_positions.push(
            lm_tex_pos[0], lm_tex_pos[1], 
            rm_tex_pos[0], rm_tex_pos[1], 
            rd_tex_pos[0], rd_tex_pos[1],

            rd_tex_pos[0], rd_tex_pos[1], 
            ld_tex_pos[0], ld_tex_pos[1], 
            lm_tex_pos[0], lm_tex_pos[1]);
    }
}

//*****************************************************************************

function set_position(out_vertices, x, y)
{
    for(let i = 0; i < out_vertices.length; i += 2)
    {
        let new_pos = [x, y];
        out_vertices[i] += new_pos[0];
        out_vertices[i + 1] += new_pos[1];
    }
}

//*****************************************************************************

function create_rectangle_with_rect(width, height, radius, offset, 
    segments = 60)
{
    let long_width = width - 2 * offset;
    let short_width = height - 2 * offset;
    let rect_height = offset - radius;

    let lu_corner = create_corner(offset * 2.0, offset * 2.0, radius, 
        segments, to_radians(90), to_radians(180));
    let ru_corner = create_corner(offset * 2.0, offset * 2.0, radius, 
        segments, to_radians(0), to_radians(90));
    let rd_corner = create_corner(offset * 2.0, offset * 2.0, radius, 
        segments, to_radians(270), to_radians(360));
    let ld_corner = create_corner(offset * 2.0, offset * 2.0, radius, 
        segments, to_radians(180), to_radians(270));

    set_position(lu_corner['top_position'], 0.0, height - offset * 2);
    set_position(lu_corner['middle_position'], 0.0, height - offset * 2);
    set_position(lu_corner['bottom_position'], 0.0, height - offset * 2);

    set_position(ru_corner['top_position'], width - offset * 2, 
        height - offset * 2);
    set_position(ru_corner['middle_position'], width - offset * 2, 
        height - offset * 2);
    set_position(ru_corner['bottom_position'], width - offset * 2, 
        height - offset * 2);

    set_position(rd_corner['top_position'], width - offset * 2, 0.0);
    set_position(rd_corner['middle_position'], width - offset * 2, 0.0);
    set_position(rd_corner['bottom_position'], width - offset * 2, 0.0);

    set_position(ld_corner['top_position'], 0.0, 0.0);
    set_position(ld_corner['middle_position'], 0.0, 0.0);
    set_position(ld_corner['bottom_position'], 0.0, 0.0);

    let top_rect = create_direction_sprite(offset, height - rect_height, 
        long_width, rect_height, segments, 'top');
    let right_rect = create_direction_sprite(width - rect_height, offset, 
        rect_height, short_width, segments, 'right');
    let bottom_rect = create_direction_sprite(offset, 0.0, long_width, 
        rect_height, segments, 'bottom');
    let left_rect = create_direction_sprite(0.0, offset, rect_height, 
        short_width, segments, 'left');

    let rect_vertices = [];
    let rect_tex_coords = [];

    update_vertices(rect_vertices, rect_tex_coords, lu_corner);
    update_vertices(rect_vertices, rect_tex_coords, ru_corner);
    update_vertices(rect_vertices, rect_tex_coords, rd_corner);
    update_vertices(rect_vertices, rect_tex_coords, ld_corner);

    update_vertices(rect_vertices, rect_tex_coords, top_rect);
    update_vertices(rect_vertices, rect_tex_coords, right_rect);
    update_vertices(rect_vertices, rect_tex_coords, bottom_rect);
    update_vertices(rect_vertices, rect_tex_coords, left_rect);

    let result = new Rect_with_rect(width, height, radius);
    result.positions = new Float32Array(rect_vertices);
    result.tex_coords = new Float32Array(rect_tex_coords);
    result.vertices_count = rect_vertices.length;

    return result;
}

//*****************************************************************************

function create_orthographic_matrix(left, right, bottom, top, near, far) 
{
    let projection_matrix = mat4.create();
	
    mat4.ortho(projection_matrix, left, right, bottom, top, near, far);
	
    return projection_matrix;
}

//*****************************************************************************

function check_shaders(...shaders) 
{
    if (shaders.length % 2 !== 0) 
    {
        console.error("Incorrect number of parameters.", 
            "Each shader must be accompanied by error text");
        return false;
    }
  
    // Обрабатываем параметры парами
    for (let i = 0; i < shaders.length; i += 2) 
    {
        const shader = shaders[i];
        const shader_error_text = shaders[i + 1];

        // Выполняем проверку шейдера
        if (!shader) 
        {
            console.error('Error in ', shader_error_text);

            return false;
        }
    }

    return true;
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