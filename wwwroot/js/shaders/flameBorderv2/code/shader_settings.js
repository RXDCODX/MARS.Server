let shader_settings = {
  time_effect: 0.7, // скорость пламени
  flame_height: 50.0, // высота пламени
  sigma: 2.0, // сигма (SIGMA), чем больше, тем больше будет размытие вокруг каждого пикселя
  luma_min: 0.01,
  luma_min_smooth: 0.01,
  is_mirror: false, // Рисовать отраженное пламя
  is_reflected: false, // Рисовать только отраженное пламя
  is_apply_to_alpha_layer: true, // очистить фон
  alpha_percentage: 100, // прозрачность пламени. 100 - макс. видимость
  flame_multi_color: {
    r: 128,
    g: 50,
    b: 200,
  }, // цвет мультипликатор для пламени (0-255)
  corner_radius: 10.0, // радиус закругления в углах
  max_fps: 60, // максимальное ФПС

  is_debug_version: false,
  is_show_fps: false,

  flame_max_height: 60.0,
  flame_min_height: 25.0,
  linear_flame_animation_speed: 500.0,
  is_linear_flame_animation: true, //плавное изменение размеров пламени
  is_listen_microphone: true,
  microphone_booster: 3.0,
};
