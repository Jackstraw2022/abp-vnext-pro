﻿using System;
using System.ComponentModel.DataAnnotations;

namespace CompanyName.ProjectName.Tenants.Dtos
{
    public class UpdateTenantInput
    {
        public Guid Id { get; set; }

        [Required(ErrorMessage = "租户名称不能为空")] public string Name { get; set; }
    }
}