﻿using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace StickerFire.Controllers
{
    public class CampaignController : Controller
    {
        public IActionResult Create()
        {
            return View();
        }
    }
}