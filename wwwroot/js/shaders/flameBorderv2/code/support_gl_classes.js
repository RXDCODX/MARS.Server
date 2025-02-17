const mat4 = glMatrix.mat4;
const vec4 = glMatrix.vec4;
const vec3 = glMatrix.vec3;
const vec2 = glMatrix.vec2;

//*****************************************************************************

class Transform
{
    position = vec3.fromValues(0.0, 0.0, 0.0);
    pivot = vec3.fromValues(0.0, 0.0, 0.0);
    in_scale = vec2.fromValues(1.0, 1.0);
    angle = 0.0;
    model;
    is_need_update = false;

    constructor(position) 
    {
        this.position = position;
        
        this.model = mat4.create();

        mat4.identity(this.model);

        this.is_need_update = true;
    }

    set_position(x, y, z = 0)
    {
        this.position[0] = x;
        this.position[1] = y;
        this.position[2] = z;

        this.is_need_update = true;
    }

    get_position()
    {
        return this.position;
    }

    move(x, y, z = 0)
    {
        this.position[0] += x;
        this.position[1] += y;
        this.position[2] += z;

        if(x != 0.0 || y != 0.0 || z != 0.0)
        {
            this.is_need_update = true;
        }
    }

    set_origin(x, y, z = 0)
    {
        this.pivot[0] = x;
        this.pivot[1] = y;
        this.pivot[2] = z;

        this.is_need_update = true;
    }

    get_origin()
    {
        return this.pivot;
    }

    set_rotation(angle)
    {
        this.angle = angle;

        this.is_need_update = true;
    }

    get_rotation()
    {
        return this.angle;
    }

    rotate(angle)
    {
        this.angle += angle;

        if(angle != 0.0)
        {
            this.is_need_update = true;
        }
    }

    set_scale(x, y)
    {
        if(x !== this.in_scale[0] || y !== this.in_scale[1])
        {
            this.is_need_update = true;
        }

        this.in_scale[0] = x;
        this.in_scale[1] = y;
    }

    scale(x, y)
    {
        this.in_scale[0] += x;
        this.in_scale[1] += y;

        if(x != 0.0 || y != 0.0)
        {
            this.is_need_update = true;
        }
    }

    get_scale()
    {
        return this.in_scale;
    }

    get_transform()
    {
        if(this.is_need_update)
        {
            mat4.identity(this.model);

            let to_pivot = vec3.create();
            vec3.negate(to_pivot, this.pivot);

            mat4.translate(this.model, this.model, this.position);
            mat4.rotateZ(this.model, this.model, to_radians(this.angle));
            mat4.scale(this.model, this.model, 
                vec3.fromValues(this.in_scale[0], this.in_scale[1], 1.0));
            mat4.translate(this.model, this.model, to_pivot);

            this.is_need_update = false;
        }

        return this.model;
    }
}

//*****************************************************************************

class Render_vertex_pack
{
    #positions;
    #tex_coords;
    #vbo_position = null;
    #vbo_tex_coords = null;
    #vao = null;
    #vertices_count = 0;
    #storage_vertices_count = 0;
    #gl = null;
    #type;

    constructor(gl, positions, tex_coords, vertices_count, type)
    {
        this.#gl = gl;
        this.#positions = new Float32Array(positions);
        this.#tex_coords = new Float32Array(tex_coords);
        this.#vertices_count = vertices_count;
        this.#storage_vertices_count = vertices_count;
        this.#type = type;

        this.#init_buffers(this.#gl);
    }

    update(start_index, vertices_count, positions, tex_coords)
    {
        const diff = vertices_count - start_index;
        const storage_diff = this.#storage_vertices_count - start_index;

        if(storage_diff < diff)
        {
            this.resize(diff - storage_diff);
        }

        for(let i = start_index; i < vertices_count; i += 2)
        {
            this.#positions[i] = positions[i];
            this.#positions[i + 1] = positions[i + 1];

            this.#tex_coords[i] = tex_coords[i];
            this.#tex_coords[i + 1] = tex_coords[i + 1];
        }

        this.#update_buffers(this.#gl);
    }

    resize(count)
    {
        if(count > this.#vertices_count)
        {
            let tmp_positions = new Float32Array(count);
            let tmp_tex_coords = new Float32Array(count);

            for(let i = 0; i < this.#vertices_count; i += 2)
            {
                tmp_positions[i] = this.#positions[i];
                tmp_positions[i + 1] = this.#positions[i + 1];

                tmp_tex_coords[i] = this.#tex_coords[i];
                tmp_tex_coords[i + 1] = this.#tex_coords[i + 1];
            }

            this.#positions = tmp_positions;
            this.#tex_coords = tmp_tex_coords;

            this.#storage_vertices_count = count;
        }
        
        this.#vertices_count = count;
    }

    clear()
    {
        this.#vertices_count = 0;
    }

    get_vao()
    {
        return this.#vao;
    }

    get_positions()
    {
        const result = this.#positions;
        return result;
    }

    get_tex_coords()
    {
        const result = this.#tex_coords;
        return result;
    }

    get_vertices_count()
    {
        const result = this.#vertices_count;
        return result;
    }

    draw(shader)
    {
        shader.use();

        shader.set_uniform_matrix('model_matrix', mat4.create());

        this.#gl.bindVertexArray(this.#vao);

        this.#gl.drawArrays(this.#type, 0, this.#vertices_count / 2);
    
        this.#gl.bindVertexArray(null);
    }

    #init_buffers(gl) 
    {
        this.#vao = gl.createVertexArray();
        gl.bindVertexArray(this.#vao);

        this.#vbo_position = gl.createBuffer();
        gl.bindBuffer(gl.ARRAY_BUFFER, this.#vbo_position);
        gl.bufferData(gl.ARRAY_BUFFER, this.#positions, gl.DYNAMIC_DRAW);

        const position_attr_location = 0;

        gl.enableVertexAttribArray(position_attr_location);
        gl.vertexAttribPointer(position_attr_location, 2, gl.FLOAT, false, 0, 0);

        this.#vbo_tex_coords = gl.createBuffer();
        gl.bindBuffer(gl.ARRAY_BUFFER, this.#vbo_tex_coords);
        gl.bufferData(gl.ARRAY_BUFFER, this.#tex_coords, gl.DYNAMIC_DRAW);

        const tex_attr_location = 1;
        
        gl.enableVertexAttribArray(tex_attr_location);
        gl.vertexAttribPointer(tex_attr_location, 2, gl.FLOAT, false, 0, 0);

        gl.bindBuffer(gl.ARRAY_BUFFER, null);
        gl.bindVertexArray(null);
    }

    #update_buffers(gl)
    {
        gl.bindBuffer(gl.ARRAY_BUFFER, this.#vbo_position);
        gl.bufferSubData(gl.ARRAY_BUFFER, 0, this.#positions);

        gl.bindBuffer(gl.ARRAY_BUFFER, this.#vbo_tex_coords);
        gl.bufferSubData(gl.ARRAY_BUFFER, 0, this.#tex_coords);
    }
}

//*****************************************************************************

class Sprite
{
    #width = 0;
    #height = 0;
    #positions;
    #tex_coords;
    #vbo_position = null;
    #vbo_tex_coords = null;
    #vao = null;
    #vertices_count = 0;
    #gl = null;
    model = new Transform([0, 0, 0]);

    constructor(gl, width, height) 
    {
        this.#width = width;
        this.#height = height;
        this.#gl = gl;

        this.#update_vertices();
        this.#init_buffers(gl);
    }

    set_size(width, height)
    {
        this.#width = width;
        this.#height = height;

        this.#update_vertices();
        this.#update_buffers(this.#gl);
    }

    get_size()
    {
        return [this.#width, this.#height];
    }

    get_positions()
    {
        const result = this.#positions;
        return result;
    }

    get_tex_coords()
    {
        const result = this.#tex_coords;
        return result;
    }

    get_vertices_count()
    {
        const result = this.#vertices_count;
        return result;
    }

    draw(shader) 
    {
        shader.use();

        this.#gl.bindVertexArray(this.#vao);

        shader.set_uniform_matrix('model_matrix', this.model.get_transform());

        this.#gl.drawArrays(this.#gl.TRIANGLE_STRIP, 0, this.#vertices_count / 2);
    
        this.#gl.bindVertexArray(null);
    }
    
    #update_vertices()
    {
        if(this.#vertices_count <= 0)
        {
            this.#vertices_count = 4 * 2;

            this.#positions = new Float32Array(this.#vertices_count);
            this.#tex_coords = new Float32Array(this.#vertices_count);
        }

        this.#positions[0] = 0.0;
        this.#positions[1] = 0.0;
        this.#positions[2] = 0.0;
        this.#positions[3] = this.#height;
        this.#positions[4] = this.#width;
        this.#positions[5] = 0.0;
        this.#positions[6] = this.#width;
        this.#positions[7] = this.#height;

        this.#tex_coords[0] = 0.0;
        this.#tex_coords[1] = 0.0;
        this.#tex_coords[2] = 0.0;
        this.#tex_coords[3] = 1.0;
        this.#tex_coords[4] = 1.0;
        this.#tex_coords[5] = 0.0;
        this.#tex_coords[6] = 1.0;
        this.#tex_coords[7] = 1.0;
    }

    #init_buffers(gl) 
    {
        this.#vao = gl.createVertexArray();
        gl.bindVertexArray(this.#vao);

        this.#vbo_position = gl.createBuffer();
        gl.bindBuffer(gl.ARRAY_BUFFER, this.#vbo_position);
        gl.bufferData(gl.ARRAY_BUFFER, this.#positions, gl.DYNAMIC_DRAW);

        const position_attr_location = 0;

        gl.enableVertexAttribArray(position_attr_location);
        gl.vertexAttribPointer(position_attr_location, 2, gl.FLOAT, false, 0, 0);

        this.#vbo_tex_coords = gl.createBuffer();
        gl.bindBuffer(gl.ARRAY_BUFFER, this.#vbo_tex_coords);
        gl.bufferData(gl.ARRAY_BUFFER, this.#tex_coords, gl.DYNAMIC_DRAW);

        const tex_attr_location = 1;
        
        gl.enableVertexAttribArray(tex_attr_location);
        gl.vertexAttribPointer(tex_attr_location, 2, gl.FLOAT, false, 0, 0);

        gl.bindBuffer(gl.ARRAY_BUFFER, null);
        gl.bindVertexArray(null);
    }

    #update_buffers(gl)
    {
        gl.bindBuffer(gl.ARRAY_BUFFER, this.#vbo_position);
        gl.bufferSubData(gl.ARRAY_BUFFER, 0, this.#positions);

        gl.bindBuffer(gl.ARRAY_BUFFER, this.#vbo_tex_coords);
        gl.bufferSubData(gl.ARRAY_BUFFER, 0, this.#tex_coords);
    }
}

//*****************************************************************************



//*****************************************************************************

class Rect_with_hole
{
    #positions;
    #tex_coords;
    #vbo_position = null;
    #vbo_tex_coords = null;
    #vao = null;
    #vertices_count = 0;
    #width = 0.0;
    #height = 0.0;
    #radius = 0.0;
    #segments = 0;
    #start_angle = 0.0;
    #end_angle = 0.0;
    #gl = null;
    model = new Transform([0, 0, 0]);

    constructor(gl, width, height, radius, start_angle = 0.0, end_angle = 0.0, 
        segments = 60)
    {
        this.#gl = gl;
        this.#width = width;
        this.#height = height;
        this.#radius = radius;
        this.#start_angle = Math.min(start_angle, end_angle);
        this.#end_angle = Math.max(start_angle, end_angle);
        this.#segments = segments;

        this.#update_vertices();
        this.#init_buffers(gl);
    }

    set_size(width, height)
    {
        this.#width = width;
        this.#height = height;

        this.#update_vertices();
        this.#update_buffers(this.#gl);
    }

    get_size()
    {
        return [this.#width, this.#height];
    }

    set_radius(radius)
    {
        this.#radius = radius;

        this.#update_vertices();
        this.#update_buffers(this.#gl);
    }

    get_radius()
    {
        return this.#radius;
    }
    
    get_segments()
    {
        return this.#segments;
    }

    get_angles()
    {
        return { begin: this.#start_angle, end: this.#end_angle };
    }

    get_positions()
    {
        //const result = this.#positions;
        //return result;
        return this.#positions;
    }

    get_tex_coords()
    {
        //const result = this.#tex_coords;
        //return result;
        return this.#tex_coords;
    }

    get_vertices_count()
    {
        //const result = this.#vertices_count;
        //return result;
        return this.#vertices_count;
    }

    draw(shader) 
    {
        shader.use();

        this.#gl.bindVertexArray(this.#vao);

        shader.set_uniform_matrix('model_matrix', this.model.get_transform());

        this.#gl.drawArrays(this.#gl.TRIANGLES, 0, this.#vertices_count / 2);

        this.#gl.bindVertexArray(null);
    }

    #update_vertices()
    {
        let circle_center = [this.#width / 2.0, this.#height / 2.0];
        let hole_positions = create_arc(circle_center, this.#radius, 
            this.#segments, this.#start_angle, this.#end_angle);
        
        let ld_rect = [0.0, 0.0];
        let lu_rect = [0.0, this.#height];
        let ru_rect = [this.#width, this.#height];
        let rd_rect = [this.#width, 0.0];

        let m_ld_rect = [circle_center[0] - this.#radius, 
            circle_center[1] - this.#radius];
        let m_lu_rect = [circle_center[0] - this.#radius, 
            circle_center[1] + this.#radius];
        let m_ru_rect = [circle_center[0] + this.#radius, 
            circle_center[1] + this.#radius];
        let m_rd_rect = [circle_center[0] + this.#radius, 
            circle_center[1] - this.#radius];

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

            direction[0] *= this.#width * 10;
            direction[1] *= this.#height * 10;

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
            let m_right_pos = is_intersect_ray_segment(circle_center, 
                direction, m_ru_rect, m_rd_rect);
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

        if(this.#vertices_count > 0)
        {
            for(let i = 0; i < this.#vertices_count; i += 2)
            {
                this.#positions[i] = rect_vertices[i];
                this.#positions[i + 1] = rect_vertices[i + 1];

                this.#tex_coords[i] = rect_tex_coords[i];
                this.#tex_coords[i + 1] = rect_tex_coords[i + 1];
            }
        }
        else
        {
            this.#positions = new Float32Array(rect_vertices);
            this.#tex_coords = new Float32Array(rect_tex_coords);
            this.#vertices_count = rect_vertices.length;
        }
    }

    #init_buffers(gl) 
    {
        this.#vao = gl.createVertexArray();
        gl.bindVertexArray(this.#vao);

        this.#vbo_position = gl.createBuffer();
        gl.bindBuffer(gl.ARRAY_BUFFER, this.#vbo_position);
        gl.bufferData(gl.ARRAY_BUFFER, this.#positions, gl.DYNAMIC_DRAW);

        const position_attr_location = 0;

        gl.enableVertexAttribArray(position_attr_location);
        gl.vertexAttribPointer(position_attr_location, 2, gl.FLOAT, false, 0, 0);

        this.#vbo_tex_coords = gl.createBuffer();
        gl.bindBuffer(gl.ARRAY_BUFFER, this.#vbo_tex_coords);
        gl.bufferData(gl.ARRAY_BUFFER, this.#tex_coords, gl.DYNAMIC_DRAW);

        const tex_attr_location = 1;
        
        gl.enableVertexAttribArray(tex_attr_location);
        gl.vertexAttribPointer(tex_attr_location, 2, gl.FLOAT, false, 0, 0);

        gl.bindBuffer(gl.ARRAY_BUFFER, null);
        gl.bindVertexArray(null);
    }

    #update_buffers(gl)
    {
        gl.bindBuffer(gl.ARRAY_BUFFER, this.#vbo_position);
        gl.bufferSubData(gl.ARRAY_BUFFER, 0, this.#positions);

        gl.bindBuffer(gl.ARRAY_BUFFER, this.#vbo_tex_coords);
        gl.bufferSubData(gl.ARRAY_BUFFER, 0, this.#tex_coords);
    }
}

//*****************************************************************************



//*****************************************************************************

class Uniform_binder
{
    #uniform_functors = new Map();

    constructor()
    {
        this.#uniform_functors.set('uniform1f', this.#set_uniform1f);
        this.#uniform_functors.set('uniform2f', this.#set_uniform2f); 
        this.#uniform_functors.set('uniform3f', this.#set_uniform3f);
        this.#uniform_functors.set('uniform4f', this.#set_uniform4f); 
        
        this.#uniform_functors.set('uniform1i', this.#set_uniform1i);
        this.#uniform_functors.set('uniform2i', this.#set_uniform2i); 
        this.#uniform_functors.set('uniform3i', this.#set_uniform3i);
        this.#uniform_functors.set('uniform4i', this.#set_uniform4i);

        this.#uniform_functors.set('uniformMatrix2fv', 
            this.#set_uniformMatrix2fv);
            this.#uniform_functors.set('uniformMatrix3fv', 
                this.#set_uniformMatrix3fv);
        this.#uniform_functors.set('uniformMatrix4fv', 
            this.#set_uniformMatrix4fv);
    }

    set_uniform(gl_contex, uniform_location, convert_int_to_float, 
        convert_array_to_matrix, ...values)
    {
        const values_count = values.length;
        
        let uniform_type = '';

        if(values.every(value => is_number(value)))
        {
            uniform_type = 'uniform' + values_count + 
                (convert_int_to_float ? 'f' : 'i');
        }
        else if (values.every(value => is_boolean(value)))
        {
            uniform_type = 'uniform' + values_count + 'i';
        }
        else if (is_array(values[0]) && !convert_array_to_matrix)
        {
            if (values[0].every(value => is_integer(value) || 
                is_boolean(value)))
            {
                uniform_type = 'uniform' + values[0].length + 
                    (convert_int_to_float ? 'f' : 'i');
            }
            else if(values[0].every(value => is_number(value)))
            {
                uniform_type = 'uniform' + values[0].length + 'f';
            }
            
            const tmp_values = values[0];
            values = tmp_values;
        }
        else if (is_matrix(values[0], 2, 2) && convert_array_to_matrix)
        {
            uniform_type = 'uniformMatrix2fv';
        }
        else if (is_matrix(values[0], 3, 3) && convert_array_to_matrix)
        {
            uniform_type = 'uniformMatrix3fv';
        }
        else if (is_matrix(values[0], 4, 4) && convert_array_to_matrix)
        {
            uniform_type = 'uniformMatrix4fv';
        }

        const uniform_functor = this.#uniform_functors.get(uniform_type);

        if(uniform_functor === null)
        {
            console.error('Unknow uniform type: ', uniform_type);
            return; 
        }
        
        uniform_functor(gl_contex, uniform_location, ...values);     
    }

    #set_uniform1f(gl_context, uniform_location, v1)
    {
        gl_context.uniform1f(uniform_location, v1);
    }

    #set_uniform2f(gl_context, uniform_location, v1, v2)
    {
        gl_context.uniform2f(uniform_location, v1, v2);
    }

    #set_uniform3f(gl_context, uniform_location, v1, v2, v3)
    {
        gl_context.uniform3f(uniform_location, v1, v2, v3);
    }

    #set_uniform4f(gl_context, uniform_location, v1, v2, v3, v4)
    {
        gl_context.uniform4f(uniform_location, v1, v2, v3, v4);
    }

    #set_uniform1i(gl_context, uniform_location, v1)
    {
        gl_context.uniform1i(uniform_location, v1);
    }

    #set_uniform2i(gl_context, uniform_location, v1, v2)
    {
        gl_context.uniform2i(uniform_location, v1, v2);
    }

    #set_uniform3i(gl_context, uniform_location, v1, v2, v3)
    {
        gl_context.uniform3i(uniform_location, v1, v2, v3);
    }

    #set_uniform4i(gl_context, uniform_location, v1, v2, v3, v4)
    {
        gl_context.uniform4i(uniform_location, v1, v2, v3, v4);
    }

    #set_uniformMatrix2fv(gl_context, uniform_location, value)
    {
        gl_context.uniformMatrix2fv(uniform_location, false, value);
    }

    #set_uniformMatrix3fv(gl_context, uniform_location, value)
    {
        gl_context.uniformMatrix3fv(uniform_location, false, value);
    }

    #set_uniformMatrix4fv(gl_context, uniform_location, value)
    {
        gl_context.uniformMatrix4fv(uniform_location, false, value);
    }
}

//*****************************************************************************



//*****************************************************************************

class Shader
{
    #shader_program;
    #status = false;
    #uniforms_locations = new Map();
    #gl;
    #uniform_binder = new Uniform_binder();

    constructor(context, vertex_source, fragment_source)
    {
        this.#gl = context;

        this.#compile_sources(vertex_source, fragment_source);
    }

    use()
    {
        if (!this.#status || !this.#shader_program) {
            console.error('Shader program is not ready to use');

            return;
        }

        if(this.#gl.getParameter(this.#gl.CURRENT_PROGRAM) === 
            this.#shader_program)
        {
            return;
        }

        this.#gl.useProgram(this.#shader_program);
    }

    set_uniform_int(uniform_name, ...values)
    {
        this.#set_uniform(uniform_name, false, false, ...values);
    }

    set_uniform_float(uniform_name, ...values)
    {
        this.#set_uniform(uniform_name, true, false, ...values);
    }

    set_uniform_matrix(uniform_name, ...values)
    {
        this.#set_uniform(uniform_name, true, true, ...values);
    }

    get_status()
    {
        return this.#status;
    }

    get_attribute_location(name)
    {
        return this.#gl.getAttribLocation(this.#shader_program, name);
    }

    #set_uniform(uniform_name, convert_int_to_float, 
        convert_array_to_matrix, ...values)
    {
        this.use();

        const uniform_location = this.#get_cached_uniform_location(
            uniform_name);

        if(uniform_location === null)
        {
            console.error(`Shader [${this.#shader_program}]` + 
                `does not contain a uniform - ${uniform_name}`);

            return;
        }

        if(values.length === 0)
        {
            console.error(`Shader [${this.#shader_program}]` + 
                `does not accept zero arguments`);
            
            return;
        }

        this.#uniform_binder.set_uniform(this.#gl, uniform_location,
            convert_int_to_float, convert_array_to_matrix, ...values);
    }

    #get_cached_uniform_location(uniform_name)
    {
        if (!this.#status) return null;

        let uniform_location = this.#uniforms_locations.get(uniform_name);

        if (!uniform_location)
        {
            uniform_location = this.#gl.getUniformLocation(
                this.#shader_program, uniform_name);
            
            if (uniform_location) 
            {
                this.#uniforms_locations.set(uniform_name, uniform_location);
            } 
            else 
            {
                console.error(`Uniform location for "${uniform_name}" not found.`);

                return null;
            }
        }

        return uniform_location;
    }

    #compile_shader(source_code, type)
    {
        const shader = this.#gl.createShader(type);
        this.#gl.shaderSource(shader, source_code);
        this.#gl.compileShader(shader);

        if (!this.#gl.getShaderParameter(shader, this.#gl.COMPILE_STATUS)) 
        {
            console.error('An error occurred compiling the shaders: ' + 
                this.#gl.getShaderInfoLog(shader));
            this.#gl.deleteShader(shader);

            return null;
        }

        return shader;
    }

    #compile_program(vertex_shader, fragment_shader)
    {
        const shader_program = this.#gl.createProgram();
        this.#gl.attachShader(shader_program, vertex_shader);
        this.#gl.attachShader(shader_program, fragment_shader);
        this.#gl.linkProgram(shader_program);
        if (!this.#gl.getProgramParameter(shader_program, 
            this.#gl.LINK_STATUS)) 
        {
            console.error('Unable to initialize the shader program: ' + 
                this.#gl.getProgramInfoLog(shaderProgram));

            return null;
        }

        return shader_program;
    }

    #check_shaders(...shaders)
    {
        if (shaders.length % 2 !== 0) 
        {
            console.error("Incorrect number of parameters.", 
                "Each shader must be accompanied by error text");
            return false;
        }
        
        for (let i = 0; i < shaders.length; i += 2) 
        {
            const shader = shaders[i];
            const shader_error_text = shaders[i + 1];
    
            if (!shader) 
            {
                console.error('Error in ', shader_error_text);
    
                return false;
            }
        }
    
        return true;
    }

    #compile_sources(vertex_source, fragment_source)
    {
        const vertex_shader = this.#compile_shader(vertex_source, 
            this.#gl.VERTEX_SHADER);

        const fragment_shader = this.#compile_shader(fragment_source, 
            this.#gl.FRAGMENT_SHADER);

        this.#status = this.#check_shaders(
            vertex_shader, 'vertex shader',
            fragment_shader, 'fragment_shader');

        if(!this.#status) return;

        this.#shader_program = this.#compile_program(vertex_shader, 
            fragment_shader);

        this.#status = this.#check_shaders(
            this.#shader_program, 'shader program');

        this.#gl.deleteShader(vertex_shader);
        this.#gl.deleteShader(fragment_shader);

        if(!this.#status)
        {
            this.#gl.deleteShader(this.#shader_program);
        }
    }
}

//*****************************************************************************



//*****************************************************************************

class Texture
{
    width = 0;
    height = 0;
    byte_size = 0;
    #id = 0;
    #native_handle;

    static #current_id = 0;

    constructor(handle, width, height, byte_size)
    {
        this.#native_handle = handle;
        this.width = width;
        this.height = height;
        this.byte_size = byte_size;
        this.#id = this.#generate_id();
    };

    get_size()
    {
        return [this.width, this.height];
    }

    get_id()
    {
        return this.#id;
    }

    get_native_handle()
    {
        return this.#native_handle;
    }

    #generate_id()
    {
        Texture.#current_id += 1;
        return Texture.#current_id;
    }
}

//*****************************************************************************



//*****************************************************************************

class Texture_loader
{
    #gl;

    constructor(gl_context)
    {
        this.#gl = gl_context;
    }

    load_texture(data, wrap_s, wrap_t, min_filter, mag_filter, on_load, 
        width, height)
    {
        if(!is_string(data) && !is_uint8array(data) && !is_image(data))
        {
            console.error('Incorrect data to load texture!');
            return null;  
        }

        const level = 0;
        const internal_format = this.#gl.RGBA;
        const tmp_width = 1;
        const tmp_height = 1;
        const border = 0;
        const src_format = this.#gl.RGBA;
        const src_type = this.#gl.UNSIGNED_BYTE;
        const pixel = new Uint8Array([0, 0, 255, 255]);

        const texture = this.#gl.createTexture();
        let result = new Texture(texture, tmp_width, tmp_height, 
            tmp_width * tmp_height * 4);

        bind_texture(this.#gl, result);

        this.#gl.texImage2D(
            this.#gl.TEXTURE_2D,
            level,
            internal_format,
            tmp_width,
            tmp_height,
            border,
            src_format,
            src_type,
            pixel
        );

        if(is_string(data))
        {
            this.#load_texture_from_file(data, result, wrap_s, wrap_t, 
                min_filter, mag_filter, on_load);
        }
        else if(is_uint8array(data))
        {
            this.#load_texture_from_array(data, result, wrap_s, wrap_t,
                min_filter, mag_filter, on_load, width, height);
        }
        else if(is_image(data))
        {
            this.#load_texture_from_image(data, result, wrap_s, wrap_t,
                min_filter, mag_filter, on_load);
        }

        return result;
    }

    update_texture(texture, data, on_update, is_need_use_new_size = false, 
        x_offset = 0, y_offset = 0, width = 0, height = 0)
    {
        if(!is_string(data) && !is_uint8array(data) && !is_image(data))
        {
            console.error('Incorrect data to update texture!');
            return null;  
        }

        bind_texture(this.#gl, texture);

        if(is_string(data))
        {
            this.#update_texture_from_file(texture, data, on_update,
                is_need_use_new_size, x_offset, y_offset);
        }
        else if(is_uint8array(data))
        {
            this.#update_texture_from_array(texture, data, on_update,
                is_need_use_new_size, x_offset, y_offset, width, height);
        }
        else if(is_image(data))
        {
            this.#update_texture_from_image(texture, data, on_update,
                is_need_use_new_size, x_offset, y_offset);
        }
    }

    #load_texture_from_file(data, texture, wrap_s, wrap_t, min_filter,
        mag_filter, on_load)
    {
        const image = new Image();

        image.onload = () => {
            this.#load_texture_from_image(image, texture, wrap_s, wrap_t,
                min_filter, mag_filter, on_load);
        }

        image.src = data;
    }

    #load_texture_from_image(data, texture, wrap_s, wrap_t, 
        min_filter, mag_filter, on_load)
    {
        this.#load_texture_from_array(data, texture, wrap_s, wrap_t,
            min_filter, mag_filter, on_load, data.width, data.height);
    }

    #load_texture_from_array(data, texture, wrap_s, wrap_t, 
        min_filter, mag_filter, on_load, width, height)
    {
        const level = 0;
        const internal_format = this.#gl.RGBA;
        const src_format = this.#gl.RGBA;
        const src_type = this.#gl.UNSIGNED_BYTE;

        bind_texture(this.#gl, texture);

        this.#gl.texImage2D(this.#gl.TEXTURE_2D, level, internal_format,
            width, height, 0, src_format, src_type, data);

        texture.width = width;
        texture.height = height;
        texture.byte_size = width * height * 4;

        this.#gl.texParameteri(this.#gl.TEXTURE_2D, 
            this.#gl.TEXTURE_WRAP_S, wrap_s);
        this.#gl.texParameteri(this.#gl.TEXTURE_2D, 
            this.#gl.TEXTURE_WRAP_T, wrap_t);
        this.#gl.texParameteri(this.#gl.TEXTURE_2D, 
            this.#gl.TEXTURE_MIN_FILTER, min_filter);
        this.#gl.texParameteri(this.#gl.TEXTURE_2D, 
            this.#gl.TEXTURE_MAG_FILTER, mag_filter);

        if(on_load)
        {
            on_load(texture, data);
        }
    }

    #update_texture_from_file(texture, data, on_update, is_need_use_new_size,
        x_offset, y_offset)
    {
        const image = new Image();

        image.onload = () => 
        {
            this.#update_texture_from_image(texture, image, on_update,
                is_need_use_new_size, x_offset, y_offset);
        }

        image.src = data;
    }

    #update_texture_from_image(texture, data, on_update, is_need_use_new_size,
        x_offset, y_offset)
    {
        this.#update_texture_from_array(texture, data, on_update,
            is_need_use_new_size, x_offset, y_offset, data.width, data.height);
    }

    #update_texture_from_array(texture, data, on_update, is_need_use_new_size,
        x_offset, y_offset, width, height)
    {
        let tex_x = x_offset ? x_offset : 0;
        let tex_y = y_offset ? y_offset : 0;

        if(!width || width <= 0 || !height || height <= 0) return;

        if(!is_need_use_new_size)
        {
            width = width < texture.width ? width : texture.width;
            height = height < texture.height ? height : texture.height;

            this.#gl.texSubImage2D(this.#gl.TEXTURE_2D, 0, tex_x, tex_y, 
                width, height, this.#gl.RGBA, this.#gl.UNSIGNED_BYTE, data);
        }
        else
        {
            const level = 0;
            const internal_format = this.#gl.RGBA;
            const src_format = this.#gl.RGBA;
            const src_type = this.#gl.UNSIGNED_BYTE;

            this.#gl.texImage2D(this.#gl.TEXTURE_2D, level, internal_format,
                width, height, 0, src_format, src_type, data);

            texture.width = width;
            texture.height = height;
            texture.byte_size = width * height * 4;
        }

        if(on_update)
        {
            on_update(texture, data);
        }
    }
}


//*****************************************************************************



//*****************************************************************************

class Texture_manager
{
    #textures = new Map();

    constructor(){};

    add(texture_name, texture)
    {
        if(!(texture instanceof Texture) || !texture)
        {
            console.error('is not texture!');
            return;
        }

        this.#textures.set(texture_name, texture);
    }

    remove(texture_name)
    {
        this.#textures.delete(texture_name);
    }

    get(texture_name)
    {
        return this.#textures.get(texture_name);
    }

    is_has(texture_name)
    {
        return this.#textures.has(texture_name);
    }
}

//*****************************************************************************