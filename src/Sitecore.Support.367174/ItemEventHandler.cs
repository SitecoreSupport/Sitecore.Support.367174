using Sitecore.Data.Fields;
using Sitecore.Data.Items;
using Sitecore.Diagnostics;
using Sitecore.Events;
using Sitecore.SecurityModel;
using Sitecore.Tasks;
using Sitecore.Xml;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Sitecore.Support.Tasks
{
    /// <summary>
    /// Represents a ItemEvent handler.
    /// </summary>
    public class ItemEventHandler : Sitecore.Tasks.ItemEventHandler
    {
        /// <summary>
        /// Called when an item has been copied.
        /// </summary>
        /// <param name="sender">
        /// The sender.
        /// </param>
        /// <param name="args">
        /// The arguments.
        /// </param>
        protected void OnItemCopied(object sender, EventArgs args)
        {
            Item item = Event.ExtractParameter(args, 1) as Item;
            Error.AssertNotNull(item, "No item in parameters");
            using (new SecurityDisabler())
            {
                this.UpdateArchiving(item, true);
                this.UpdateReminder(item, true);
            }
        }

        /// <summary>
        /// Called when the item has been deleted.
        /// </summary>
        /// <param name="sender">
        /// The sender.
        /// </param>
        /// <param name="args">
        /// The arguments.
        /// </param>
        protected void OnItemDeleted(object sender, EventArgs args)
        {
            Item item = Event.ExtractParameter(args, 0) as Item;
            Error.AssertNotNull(item, "No item in parameters");
            using (new SecurityDisabler())
            {
                Globals.TaskDatabase.RemoveItemTasks(item);
            }
        }

        /// <summary>
        /// Called when the item is about to be saved.
        /// </summary>
        /// <param name="sender">
        /// The sender.
        /// </param>
        /// <param name="args">
        /// The arguments.
        /// </param>
        protected void OnItemSaving(object sender, EventArgs args)
        {
            Item item = Event.ExtractParameter(args, 0) as Item;
            Error.AssertNotNull(item, "No item in parameters");
            using (new SecurityDisabler())
            {
                this.UpdateArchiving(item, false);
                this.UpdateReminder(item, false);
            }
        }

        /// <summary>
        /// Called when the item has been saved.
        /// </summary>
        /// <param name="sender">
        /// The sender.
        /// </param>
        /// <param name="args">
        /// The arguments.
        /// </param>
        [Obsolete("Use OnItemSaving event subscriber instead.")]
        protected void OnItemSaved(object sender, EventArgs args)
        {
            this.OnItemSaving(sender, args);
        }

        /// <summary>
        /// Gets the reminder parameters.
        /// </summary>
        /// <param name="recipients">
        /// The recipients.
        /// </param>
        /// <param name="text">
        /// The text to send.
        /// </param>
        /// <returns>
        /// The get reminder parameters.
        /// </returns>
        private string GetReminderParameters(Field recipients, Field text)
        {
            Packet packet = new Packet(false);
            packet.StartElement("r");
            packet.AddElement("to", recipients.Value, new string[0]);
            packet.AddElement("txt", text.Value, new string[0]);
            packet.EndElement();
            return packet.OuterXml;
        }

        /// <summary>
        /// Updates the archiving.
        /// </summary>
        /// <param name="item">The item to update.</param>
        /// <param name="force">If set to <c>true</c>, the update will be performed even if the
        /// items archiving properties were not changed.</param>
        private void UpdateArchiving(Item item, bool force)
        {
            DateField dateField = item.Fields[FieldIDs.ArchiveDate];
            if (force || dateField.InnerField.IsModified)
            {
                DateTime dateTime = dateField.DateTime;
                if (dateTime > DateTime.MinValue && this.databases.Contains(item.Database.Name, new ItemEventHandler.cmp()))
                {
                    ArchiveTask archiveTask = new ArchiveTask(dateTime);
                    archiveTask.ItemID = item.ID;
                    archiveTask.DatabaseName = item.Database.Name;
                    ArchiveTask archiveTask2 = archiveTask;
                    Globals.TaskDatabase.UpdateItemTask(archiveTask2, true);
                    return;
                }
                Globals.TaskDatabase.RemoveItemTasks(item, typeof(ArchiveTask));
            }
        }

        /// <summary>
        /// Updates the reminder.
        /// </summary>
        /// <param name="item">
        /// The item to update reminder for.
        /// </param>
        /// <param name="force">
        /// If set to <c>true</c>, the update will be performed even if the 
        /// items reminder properties were not changed.
        /// </param>
        private void UpdateReminder(Item item, bool force)
        {
            DateField dateField = item.Fields[FieldIDs.ReminderDate];
            Field field = item.Fields[FieldIDs.ReminderRecipients];
            Field field2 = item.Fields[FieldIDs.ReminderText];
            if (force || dateField.InnerField.IsModified || field.IsModified || field2.IsModified)
            {
                DateTime dateTime = dateField.DateTime;
                if (dateTime > DateTime.MinValue && this.databases.Contains(item.Database.Name, new ItemEventHandler.cmp()))
                {
                    EmailReminderTask emailReminderTask = new EmailReminderTask(dateTime);
                    emailReminderTask.ItemID = item.ID;
                    emailReminderTask.DatabaseName = item.Database.Name;
                    emailReminderTask.Parameters = this.GetReminderParameters(field, field2);
                    EmailReminderTask emailReminderTask2 = emailReminderTask;
                    Globals.TaskDatabase.UpdateItemTask(emailReminderTask2, true);
                    return;
                }
                Globals.TaskDatabase.RemoveItemTasks(item, typeof(EmailReminderTask));
            }
        }
        private readonly List<string> databases = new List<string>();
        public List<string> Databases
        {
            get
            {
                return this.databases;
            }
        }
        private class cmp : IEqualityComparer<string>
        {
            public bool Equals(string str1, string str2)
            {
                return str1.Equals(str2, StringComparison.InvariantCultureIgnoreCase);
            }

            public int GetHashCode(string str)
            {
                return str.GetHashCode();
            }
        }
    }
}