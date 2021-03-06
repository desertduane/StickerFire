﻿using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using StickerFire.Data;
using StickerFire.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using System.IO;

namespace StickerFire.Controllers
{
    [Authorize]
    public class CampaignController : Controller
    {
        //Dependancy Injection for DB context and user manager
        private readonly StickerFireDbContext _Context;
        private readonly UserManager<ApplicationUser> _user;

        //Cunstructor to require a DbContext 
        public CampaignController(UserManager<ApplicationUser> userManager, StickerFireDbContext context)
        {
            _Context = context;
            _user = userManager;
        }

        //Index Gathering all campaigns from the Context
        public async Task<IActionResult> AdminIndex(Category? category, string searchString)
        {
            //Campaign search by title
            if (!String.IsNullOrEmpty(searchString))
            {
                return View(await _Context.Campaign.Where(c => c.Title.Contains(searchString)).ToListAsync());
            }
            //Campaign search by category
            if (category.HasValue)
            {
                return View(await _Context.Campaign.Where(c => c.Category == category).ToListAsync());
            }

            //Campaigns all
            return View(await _Context.Campaign.ToListAsync());
        }

        [AllowAnonymous]
        //Index Gathering all campaigns from the Context
        public async Task<IActionResult> Index(Category? category, string searchString)
        {
            //Campaign search by title
            if (!String.IsNullOrEmpty(searchString))
            {
                return View(await _Context.Campaign.Where(c => c.Title.Contains(searchString)).ToListAsync());
            }
            //Campaign search by category
            if (category.HasValue)
            {
                return View(await _Context.Campaign.Where(c => c.Category == category).ToListAsync());
            }

            //Campaigns all
            return View(await _Context.Campaign.ToListAsync());
        }

        //MyCampaigns: This will navigate to the users campaigns
        public async Task<IActionResult> MyCampaigns()
        {
            //Get current user
            string userEmail = HttpContext.User.Identity.Name;
            ApplicationUser user = await _user.FindByEmailAsync(userEmail);
           
            //Get users campaigns
            var myCampaigns = await _Context.Campaign.Where(c => c.OwnerID == user.Id).ToListAsync();

            return View(myCampaigns);
        }
        [AllowAnonymous]
        //View single campaign
        public async Task<IActionResult> ViewCampaign(int id)
        {
            //Get campaign by ID
            Campaign myCampaigns = await _Context.Campaign.FirstOrDefaultAsync(c => c.ID == id);

            //Increment the campaigns views
            myCampaigns.Views++;

            //Save the change with the edit action
            await Edit(myCampaigns.ID, myCampaigns);

            //Return the selected campaign to the view
            return View(myCampaigns);
        }

        //Increments the vote counter on the selected campaign.  Works similarly to the view incrementer.
        public async Task<IActionResult> Vote(int id)
        {
            Campaign myCampaigns = await _Context.Campaign.FirstOrDefaultAsync(c => c.ID == id);
            myCampaigns.Votes++;
            await Edit(myCampaigns.ID, myCampaigns);

            return View("ViewCampaign", myCampaigns);
        }

        //this gets the filepath from the user to pass to the blob
        [HttpPost("UploadFiles")]
        [ValidateAntiForgeryToken]
        public async Task<string> PostFile(IFormFile file)
        {

            // full path to file in temp location
            var filePath = Path.GetTempFileName();

            //Creates a stream that saves file to temporary location for async upload to Azure
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }   
            //returns the temporary file location
            return filePath;
        }

        //Get the create form View
        public IActionResult Create()
        {
            return View();
        }

        //Post method to bind the entered campaign information into the database with AntiForgery Token
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("ID,OwnerID,Votes,Views,Title,ImgPath,Description,DenyMessage,Published,Active,Category,Status")]Campaign campaign, IFormFile file)
        {
            //Get temporary file from the PostFile action
            var path = await PostFile(file);
            //Get current user
            string userEmail = HttpContext.User.Identity.Name;

            //get the current user
            ApplicationUser user = await _user.FindByEmailAsync(userEmail);

            //Check the information from the form for validity
            if (ModelState.IsValid)
            {
                //Save defaults to campaign properties
                campaign.OwnerID = user.Id;
                campaign.Active = true;

                //Upload to Azure
                var title = campaign.Title;
                await Blob.MakeAContainer(user.Id);
                await Blob.UploadBlob(user.Id, title, path);

                //Save final image path to the campaign model.
                campaign.ImgPath = Blob.GetBlobUrl(user.Id, campaign.Title);

                //Save new campaign to DB
                _Context.Add(campaign);
                await _Context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            return View();
        }


        //This Method will find the selected campaing and render the page for editing
        public async Task<IActionResult> Edit(int? id)
        {
            //Checks that campaign exists
            if (id == null)
            {
                return NotFound();
            }

            //Get selected campaign
            var campaign = await _Context.Campaign
                .SingleOrDefaultAsync(c => c.ID == id);

            if (campaign == null)
            {
                return NotFound();
            }

            return View(campaign);
        }

        //Post for the Edit method with Antiforgery Token
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("ID,OwnerID,Votes,Views,Title,ImgPath,Description,DenyMessage,Published,Active,Category,Status")]Campaign campaign)
        {
            //Check if exists
            if (id != campaign.ID)
            {
                return NotFound();
            }
            //Save edits
            if (ModelState.IsValid)
            {
                _Context.Update(campaign);
                await _Context.SaveChangesAsync();
                return RedirectToAction(nameof(AdminIndex));
            }
            return View(campaign);
        }

        //Get Method that will grab the campaign selected for deletion
        public async Task<IActionResult> Delete(int? id)
        {
            //Check if exists
            if (id == null)
            {
                return NotFound();
            }

            //Get Selected to delete
            var campaign = await _Context.Campaign
                .SingleOrDefaultAsync(m => m.ID == id);
            if (campaign == null)
            {
                return NotFound();
            }
            return View(campaign);
        }

        //This will delete the selected campaign
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var campaign = await _Context.Campaign
                .SingleOrDefaultAsync(m => m.ID == id);

            _Context.Campaign.Remove(campaign);
            await _Context.SaveChangesAsync();
            return RedirectToAction(nameof(AdminIndex));
        }

    }
}
