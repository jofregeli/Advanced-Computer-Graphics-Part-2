#include "material.h"

#include "application.h"

#include <istream>
#include <fstream>
#include <algorithm>



//------------------------------------------------------------------------------------------------
//------------------------------------------------------------------------------------------------
//------------------------------------------------------------------------------------------------

// Volume Material

VolumeMaterial::VolumeMaterial(glm::vec4 color)
{
	this->color = color;


	this->shader = Shader::Get("res/shaders/basic.vs", "res/shaders/volume.fs");


}

VolumeMaterial::~VolumeMaterial() {}

void VolumeMaterial::setUniforms(Camera* camera, glm::mat4 model)
{
	// Upload node uniforms
	this->shader->setUniform("u_viewprojection", camera->viewprojection_matrix);
	this->shader->setUniform("u_camera_position", camera->eye);
	this->shader->setUniform("u_model", model);
	this->shader->setUniform("u_absorption_coefficient", this->absorption_coefficient);
	this->shader->setUniform("ub_color", Application::instance->background_color);	
	this->shader->setUniform("stepLength", stepLength);
	this->shader->setUniform("choose_value1", choose_value1);
	this->shader->setUniform("choose_value2", choose_value2);
	this->shader->setUniform("choose_value3", choose_value3);
	this->shader->setUniform("u_emission_color", this->color);
	this->shader->setUniform("ac", this->absorptionCoefficient);
	this->shader->setUniform("use_random_le", use_random_le);





	
}

void VolumeMaterial::render(Mesh* mesh, glm::mat4 model, Camera* camera)
{
	if (mesh && this->shader) {
		// Enable shader
		this->shader->enable();

		// Upload uniforms
		this->shader->setUniform("u_min", mesh->aabb_min);
		this->shader->setUniform("u_max", mesh->aabb_max);
		setUniforms(camera, model);

		// Do the draw call
		mesh->render(GL_TRIANGLES);

		this->shader->disable();
	}
}

void VolumeMaterial::renderInMenu()
{
	ImGui::Text("Material Type: %s", std::string("Flat").c_str());

	ImGui::ColorEdit3("Color", (float*)&this->color);

	ImGui::ColorEdit3("Background Color", (float*)&Application::instance->background_color);
	ImGui::SliderFloat("Step Length", &this->stepLength, 0.01f, 1.f);
	ImGui::SliderFloat("Choose Value 1", &this->choose_value1, 0.01f, 1.f);
	ImGui::SliderFloat("Choose Value 2", &this->choose_value2, 0.01f, 1.f);
	ImGui::SliderFloat("Choose Value 3", &this->choose_value3, 0.01f, 1.f);
	

	const char* shader_options[] = { "Homogeneous Absorption", "Heterogeneous Absorption", "Amborption + Emission" };

	if (ImGui::Combo("Shader Type", &using_shader, shader_options, 3))
	{

		// Recarga el shader correspondiente
		if (using_shader == 0) {
			this->shader = Shader::Get("res/shaders/basic.vs", "res/shaders/volume.fs");
		}
		else if (using_shader == 1) {
			this->shader = Shader::Get("res/shaders/basic.vs", "res/shaders/volume_2.fs");
		}
		else {
			this->shader = Shader::Get("res/shaders/basic.vs", "res/shaders/volume_3.fs");
		}
		
	}

	ImGui::SliderFloat("Absorption Coefficient", &this->absorptionCoefficient, 0.01f, 10.f);
	ImGui::Checkbox("Use Random Light Emission", &this->use_random_le);
	
}



//------------------------------------------------------------------------------------------------
//------------------------------------------------------------------------------------------------
//------------------------------------------------------------------------------------------------









FlatMaterial::FlatMaterial(glm::vec4 color)
{
	this->color = color;
	this->shader = Shader::Get("res/shaders/basic.vs", "res/shaders/flat.fs");
}

FlatMaterial::~FlatMaterial() { }

void FlatMaterial::setUniforms(Camera* camera, glm::mat4 model)
{
	// Upload node uniforms
	this->shader->setUniform("u_viewprojection", camera->viewprojection_matrix);
	this->shader->setUniform("u_camera_position", camera->eye);
	this->shader->setUniform("u_model", model);

	this->shader->setUniform("u_color", this->color);
}

void FlatMaterial::render(Mesh* mesh, glm::mat4 model, Camera* camera)
{
	if (mesh && this->shader) {
		// Enable shader
		this->shader->enable();

		// Upload uniforms
		setUniforms(camera, model);

		// Do the draw call
		mesh->render(GL_TRIANGLES);

		this->shader->disable();
	}
}

void FlatMaterial::renderInMenu()
{
	ImGui::Text("Material Type: %s", std::string("Flat").c_str());

	ImGui::ColorEdit3("Color", (float*)&this->color);
}

WireframeMaterial::WireframeMaterial()
{
	this->color = glm::vec4(1.f);
	this->shader = Shader::Get("res/shaders/basic.vs", "res/shaders/flat.fs");
}

WireframeMaterial::~WireframeMaterial() { }

void WireframeMaterial::render(Mesh* mesh, glm::mat4 model, Camera* camera)
{
	if (this->shader && mesh)
	{
		glPolygonMode(GL_FRONT_AND_BACK, GL_LINE);
		glDisable(GL_CULL_FACE);

		// Enable shader
		this->shader->enable();

		// Upload material specific uniforms
		setUniforms(camera, model);

		// Do the draw call
		mesh->render(GL_TRIANGLES);

		glEnable(GL_CULL_FACE);
		glPolygonMode(GL_FRONT_AND_BACK, GL_FILL);
	}
}

StandardMaterial::StandardMaterial(glm::vec4 color)
{
	this->color = color;
	this->base_shader = Shader::Get("res/shaders/basic.vs", "res/shaders/basic.fs");
	this->normal_shader = Shader::Get("res/shaders/basic.vs", "res/shaders/normal.fs");
	this->shader = this->base_shader;
}

StandardMaterial::~StandardMaterial() { }

void StandardMaterial::setUniforms(Camera* camera, glm::mat4 model)
{
	// Upload node uniforms
	this->shader->setUniform("u_viewprojection", camera->viewprojection_matrix);
	this->shader->setUniform("u_camera_position", camera->eye);
	this->shader->setUniform("u_model", model);

	this->shader->setUniform("u_color", this->color);

	if (this->texture) {
		this->shader->setUniform("u_texture", this->texture, 0);
	}
}

void StandardMaterial::render(Mesh* mesh, glm::mat4 model, Camera* camera)
{
	bool first_pass = true;
	if (mesh && this->shader)
	{
		// Enable shader
		this->shader->enable();

		// Multi pass render
		int num_lights = (int)Application::instance->light_list.size();
		for (int nlight = -1; nlight < num_lights; nlight++)
		{
			if (nlight == -1) { nlight++; } // hotfix

			// Upload uniforms
			setUniforms(camera, model);

			// Upload light uniforms
			if (!first_pass) {
				glBlendFunc(GL_SRC_ALPHA, GL_ONE);
				glDepthFunc(GL_LEQUAL);
			}
			this->shader->setUniform("u_ambient_light", Application::instance->ambient_light * (float)first_pass);

			if (num_lights > 0) {
				Light* light = Application::instance->light_list[nlight];
				light->setUniforms(this->shader, model);
			}
			else {
				// Set some uniforms in case there is no light
				this->shader->setUniform("u_light_intensity", 1.f);
				this->shader->setUniform("u_light_shininess", 1.f);
				this->shader->setUniform("u_light_color", glm::vec4(0.f));
			}

			// Do the draw call
			mesh->render(GL_TRIANGLES);
            
			first_pass = false;
		}

		// Disable shader
		this->shader->disable();
	}
}

void StandardMaterial::renderInMenu()
{
	ImGui::Text("Material Type: %s", std::string("Standard").c_str());

	if (ImGui::Checkbox("Show Normals", &this->show_normals)) {
		if (this->show_normals) {
			this->shader = this->normal_shader;
		}
		else {
			this->shader = this->base_shader;
		}
	}

	if (!this->show_normals) ImGui::ColorEdit3("Color", (float*)&this->color);
}