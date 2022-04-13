﻿using CNet.Common;
using log4net;
using log4net.Config;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CNet.Web.Api
{
    public class Startup
    {
        public readonly string anyAllowSpecificOrigins = "any";//解决跨域
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;

            LogConfig();

        }
        
        //日志配置
        private static void LogConfig()
        {
            // //log4Net
            var logTypes = Enum.GetValues(typeof(LogType));
            foreach (LogType logType in logTypes)
            {
                var repository = LogManager.CreateRepository(logType.ToString());
                XmlConfigurator.Configure(repository, new FileInfo(Environment.CurrentDirectory + "/log4net.config"));
            }
        }

        public IConfiguration Configuration { get; }

        //依赖注入服务
        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            //1.全局异常 2.Json 日期格式化
            services
                .AddMvc(o => { o.Filters.Add(typeof(WebApiExceptionAttribute)); o.EnableEndpointRouting = false; });
            services.AddControllers().AddNewtonsoftJson(options =>
            {
                options.SerializerSettings.ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore;
                options.SerializerSettings.DateFormatString = "yyyy-MM-dd HH:mm:ss";
            });

            //参考 https://www.cnblogs.com/aishangyipiyema/p/9262642.html
            JWTConfig(services);

            SwaggerConfig(services);

            services.AddCors(options =>
            {

                // this defines a CORS policy called "default"

                options.AddPolicy("default", policy =>
                {

                    policy.WithOrigins("*").AllowAnyHeader().AllowAnyMethod();

                });

            });
            //services.AddControllers();

            services.AddControllers(options =>
            {
                //options.Filters.Add(typeof(ApiExceptionFilter));
            });
            
            //同步读取body的方式需要ConfigureServices中配置允许同步读取IO流，否则可能会抛出异常 Synchronous operations are disallowed. Call ReadAsync or set AllowSynchronousIO to true instead.
            services.Configure<KestrelServerOptions>(x => x.AllowSynchronousIO = true)
                        .Configure<IISServerOptions>(x => x.AllowSynchronousIO = true);
            //解决跨域
            //services.AddCors(options =>
            //{
            //    options.AddPolicy(anyAllowSpecificOrigins, corsbuilder =>
            //    {
            //        var corsPath = Configuration.GetSection("CorsPaths").GetChildren().Select(p => p.Value).ToArray();
            //        corsbuilder.WithOrigins(corsPath)
            //        .AllowAnyMethod()
            //        .AllowAnyHeader()
            //        .AllowCredentials();//指定处理cookie
            //    });
            //});
        }

        private static void SwaggerConfig(IServiceCollection services)
        {
            //注册Swagger生成器，定义一个和多个Swagger 文档
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = "CNet API",
                    Version = "v1",
                    Description = "CNet基础框架API",
                });
                c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    In = ParameterLocation.Header,
                    Type = SecuritySchemeType.ApiKey,
                    Description = "直接在下框中输入Bearer {token}（注意两者之间是一个空格）",
                    Name = "Authorization",
                    BearerFormat = "JWT",
                    Scheme = "Bearer"
                });
                //不加AddSecurityRequirement请求头不会有authorization
                c.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                  {
                    new OpenApiSecurityScheme
                    {
                      Reference=new OpenApiReference
                      {
                        Type=ReferenceType.SecurityScheme,
                        Id="Bearer"
                      }
                    },
                    new string[] {}
                  }
                });
                //swagger中控制请求的时候发是否需要在url中增加accesstoken
                // c.OperationFilter<AuthTokenHeaderParameter>();

                // 为 Swagger JSON and UI设置xml文档注释路径
                //HttpContext.Current.Request.PhysicalApplicationPath
                var basePath = Path.GetDirectoryName(typeof(Program).Assembly.Location);//获取应用程序所在目录（绝对，不受工作目录影响，建议采用此方法获取路径）
                var xmlPath = Path.Combine(basePath, "CNet.Web.Api.xml");
                c.IncludeXmlComments(xmlPath);
            });
        }


        /// <summary>
        /// 使用 Microsoft.AspNetCore.Authentication.JwtBearer
        /// </summary>
        /// <param name="services"></param>
        private void JWTConfig(IServiceCollection services)
        {
            services.Configure<JwtSeetings>(Configuration.GetSection("JwtSeetings"));
            var jwtSeetings = new JwtSeetings();
            //绑定jwtSeetings
            Configuration.Bind("JwtSeetings", jwtSeetings);
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;

            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = jwtSeetings.Issuer,
                    ValidAudience = jwtSeetings.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSeetings.SecretKey))
                };
            });
        }

        //中间件
        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ILoggerFactory loggerFactory)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseSwagger();
                app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "CNet.Web.Api v1"));
            }
            //Microsfot.Extensions.Logging.Log4Net.AspNetCore 需添加
            //loggerFactory.AddLog4Net(Environment.CurrentDirectory + "//log4net.config");
            //app.UseHttpsRedirection();//会跳转跨域时不要使用

            
            app.UseRouting();
            // app.UseMvc();

            app.UseStaticFiles();
            ////jwt认证 需要在app.UseMvc()前调用
            app.UseAuthentication();//不添加报401
           app.UseCors("default");
          // app.UseCors(anyAllowSpecificOrigins);//支持跨域：允许特定来源的主机访问
            app.UseAuthorization();

            //request.body的长度总是为0
            app.Use(next => context =>
            {
                context.Request.EnableBuffering();
                return next(context);
            });

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers().RequireCors("default");
            });

        }
    }
}
