const mat4 = glMatrix.mat4;
const mat2 = glMatrix.mat2;
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
        //let result = vec3.fromValues(this.pivot[0], this.pivot[1], 0.0);
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

    get_transfrom()
    {
        if(this.is_need_update)
        {
            mat4.identity(this.model);

            let to_pivot = vec3.create();
            //vec3.negate(to_pivot, 
            //    vec3.fromValues(this.pivot[0], this.pivot[1], 0.0));

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



//*****************************************************************************

class Ring 
{
    positions;
    tex_coords;
    vbo_position = null;
    vbo_tex_coords = null;
    vao = null;
    vertices_count = 0;
    model = new Transform([0, 0, 0]);

    constructor(inner_radius, outer_radius, segments_count, start_angle, 
        end_angle) 
    {
        this.inner_radius = inner_radius;
        this.outer_radius = outer_radius;
        this.segments_count = segments_count;
        this.start_angle = start_angle;
        this.end_angle = end_angle;
    }

    init_buffers(gl, shader) 
    {
        gl.useProgram(shader);
        
        this.vao = gl.createVertexArray();
        gl.bindVertexArray(this.vao);

        this.vbo_position = gl.createBuffer();
        gl.bindBuffer(gl.ARRAY_BUFFER, this.vbo_position);
        gl.bufferData(gl.ARRAY_BUFFER, this.positions, gl.STATIC_DRAW);

        const position_attr_location = gl.getAttribLocation(shader, 'aVertexPosition');

        gl.enableVertexAttribArray(position_attr_location);
        gl.vertexAttribPointer(position_attr_location, 2, gl.FLOAT, false, 0, 0);

        this.vbo_tex_coords = gl.createBuffer();
        gl.bindBuffer(gl.ARRAY_BUFFER, this.vbo_tex_coords);
        gl.bufferData(gl.ARRAY_BUFFER, this.tex_coords, gl.STATIC_DRAW);

        const tex_attr_location = gl.getAttribLocation(shader, 'aTexPosition');
        
        gl.enableVertexAttribArray(tex_attr_location);
        gl.vertexAttribPointer(tex_attr_location, 2, gl.FLOAT, false, 0, 0);

        gl.bindBuffer(gl.ARRAY_BUFFER, null);
        gl.bindVertexArray(null);
    }
    
    draw(gl, shader) 
    {
        gl.useProgram(shader);

        gl.bindVertexArray(this.vao);

        let model_loc = get_uniform_location(gl, shader, 'model_matrix');

        gl.uniformMatrix4fv(model_loc, false, this.model.get_transfrom());

        gl.drawArrays(gl.TRIANGLE_STRIP, 0, this.vertices_count / 2);

        gl.bindVertexArray(null);
    }
}

//*****************************************************************************



//*****************************************************************************

class Circle 
{
    positions;
    tex_coords;
    vbo_position = null;
    vbo_tex_coords = null;
    vao = null;
    vertices_count = 0;
    model = new Transform([0, 0, 0]);

    constructor(radius, segments_count) 
    {
        this.radius = radius;
        this.segments_count = segments_count;
    }

    init_buffers(gl, shader) 
    {
        gl.useProgram(shader);
        
        this.vao = gl.createVertexArray();
        gl.bindVertexArray(this.vao);

        this.vbo_position = gl.createBuffer();
        gl.bindBuffer(gl.ARRAY_BUFFER, this.vbo_position);
        gl.bufferData(gl.ARRAY_BUFFER, this.positions, gl.STATIC_DRAW);

        const position_attr_location = gl.getAttribLocation(shader, 'aVertexPosition');

        gl.enableVertexAttribArray(position_attr_location);
        gl.vertexAttribPointer(position_attr_location, 2, gl.FLOAT, false, 0, 0);

        this.vbo_tex_coords = gl.createBuffer();
        gl.bindBuffer(gl.ARRAY_BUFFER, this.vbo_tex_coords);
        gl.bufferData(gl.ARRAY_BUFFER, this.tex_coords, gl.STATIC_DRAW);

        const tex_attr_location = gl.getAttribLocation(shader, 'aTexPosition');
        
        gl.enableVertexAttribArray(tex_attr_location);
        gl.vertexAttribPointer(tex_attr_location, 2, gl.FLOAT, false, 0, 0);

        gl.bindBuffer(gl.ARRAY_BUFFER, null);
        gl.bindVertexArray(null);
    }
    
    draw(gl, shader) 
    {
        gl.useProgram(shader);

        gl.bindVertexArray(this.vao);

        let model_loc = get_uniform_location(gl, shader, 'model_matrix');
        
        gl.uniformMatrix4fv(model_loc, false, this.model.get_transfrom());

        gl.drawArrays(gl.TRIANGLE_FAN, 0, this.vertices_count / 2);

        gl.bindVertexArray(null);
    }
}

//*****************************************************************************



//*****************************************************************************

class Texture_loader
{
    constructor(){};

    load_texture(context, data, on_load_functor)
    {

    }

    update_texture_from_data(context, texture, data, on_update_functor)
    {
        
    }

    update_texture_from_image(context, texture, image, on_update_functor)
    {

    }    
}

//*****************************************************************************