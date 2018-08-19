// <copyright>
// Copyright Pillars Inc.
// </copyright>
//
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Linq;

using Rock;
using Rock.Attribute;
using Rock.Data;
using Rock.Model;
using Rock.Web.Cache;
using Rock.Workflow;

namespace rocks.pillars.WorkflowActions.Workflow.Action.Prayer
{
    /// <summary>
    /// Sets an attribute's value to the selected person 
    /// </summary>
    [ActionCategory( "Pillars: Prayer" )]
    [Description( "Adds a prayer request." )]
    [Export( typeof( ActionComponent ) )]
    [ExportMetadata( "ComponentName", "Request Add" )]

    [WorkflowAttribute( "Person", "Workflow attribute that contains the person to add the request for.", true, "", "", 2, null,
        new string[] { "Rock.Field.Types.PersonFieldType" } )]
    [WorkflowTextOrAttribute( "Prayer Request Text", "Attribute Value", "The prayer request text. <span class='tip tip-lava'></span>", true, "", "", 3, "PrayerRequestText",
        new string[] { "Rock.Field.Types.TextFieldType", "Rock.Field.Types.MemoFieldType" } )]
    [WorkflowTextOrAttribute( "Public", "Attribute Value", "Should the prayer request be a public request (True/False). <span class='tip tip-lava'></span>", false, "", "", 4, "Public",
        new string[] { "Rock.Field.Types.TextFieldType", "Rock.Field.Types.BooleanFieldType" } )]
    [WorkflowAttribute( "Campus", "Workflow attribute that contains the campus that prayer request should be associated with.", false, "", "", 5, null,
        new string[] { "Rock.Field.Types.CampusFieldType" } )]
    [CategoryField( "Category", "What category should the prayer request belong to?", false, "Rock.Model.PrayerRequest", "", "", true, "", "", 6 )]
    [WorkflowAttribute( "Result Attribute", "An optional attribute to set to the prayer request id that is created.", false, "", "", 7 )]

    public class RequestAdd : ActionComponent
    {
        /// <summary>
        /// Executes the specified workflow.
        /// </summary>
        /// <param name="rockContext">The rock context.</param>
        /// <param name="action">The action.</param>
        /// <param name="entity">The entity.</param>
        /// <param name="errorMessages">The error messages.</param>
        /// <returns></returns>
        public override bool Execute( RockContext rockContext, WorkflowAction action, Object entity, out List<string> errorMessages )
        {
            errorMessages = new List<string>();

            // determine the person
            Person person = null;
            var personAliasGuid = GetAttributeValue( action, "Person", true ).AsGuidOrNull();
            {
                person = new PersonAliasService( rockContext ).Queryable()
                    .Where( a => a.Guid.Equals( personAliasGuid.Value ) )
                    .Select( a => a.Person )
                    .FirstOrDefault();
            }
            if ( person == null || !person.PrimaryAliasId.HasValue )
            {
                errorMessages.Add( "The Person for the prayer request could not be determined or found!" );
            }

            // determine the contents of prayer request
            var requestText = GetAttributeValue( action, "PrayerRequestText", true );
            if ( requestText.IsNullOrWhiteSpace() )
            {
                errorMessages.Add( "The contents of the prayer request could not be determined or found!" );
            }

            // determine if public
            bool isPublic = GetAttributeValue( action, "Public", true ).AsBoolean();

            // determine the campus 
            CampusCache campus = CampusCache.Read( GetAttributeValue( action, "Campus", true ).AsGuid() );

            // Add request
            if ( !errorMessages.Any() )
            {
                var requestService = new PrayerRequestService( rockContext );

                PrayerRequest prayerRequest = new PrayerRequest { Id = 0, IsActive = true, IsApproved = false, AllowComments = false };
                requestService.Add( prayerRequest );
                prayerRequest.EnteredDateTime = RockDateTime.Now;
                prayerRequest.RequestedByPersonAliasId = person.PrimaryAliasId;
                prayerRequest.FirstName = person.NickName;
                prayerRequest.LastName = person.LastName;
                prayerRequest.Email = person.Email;
                prayerRequest.Text = requestText;
                prayerRequest.IsPublic = isPublic;
                prayerRequest.CampusId = campus != null ? campus.Id : (int?)null;

                var categoryGuid = GetAttributeValue( action, "Category" ).AsGuidOrNull();
                if ( categoryGuid.HasValue )
                {
                    prayerRequest.CategoryId = new CategoryService( rockContext ).Get( categoryGuid.Value )?.Id;
                }

                rockContext.SaveChanges();

                if ( prayerRequest.Id > 0 )
                {
                    string resultValue = prayerRequest.Id.ToString();
                    var attribute = SetWorkflowAttributeValue( action, "ResultAttribute", resultValue );
                    if ( attribute != null )
                    {
                        action.AddLogEntry( string.Format( "Set '{0}' attribute to '{1}'.", attribute.Name, resultValue ) );
                    }
                }
            }

            errorMessages.ForEach( m => action.AddLogEntry( m, true ) );

            return !errorMessages.Any();
        }

    }
}