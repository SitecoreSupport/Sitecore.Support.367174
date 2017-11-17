using Sitecore.Data.Fields;
using Sitecore.Data.Items;
using Sitecore.Diagnostics;
using Sitecore.Events;
using Sitecore.SecurityModel;
using Sitecore.Xml;
using System;

namespace Sitecore.Tasks
{
    /// <summary>
    /// Represents a ItemEvent handler.
    /// </summary>
    public class ItemEventHandler
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
            Assert.ArgumentNotNull(item, "item");
            DateField dateField = item.Fields[FieldIDs.ArchiveDate];
            if (force || dateField.InnerField.IsModified)
            {
                DateTime dateTime = dateField.DateTime;
                ArchiveItem archiveItem = new ArchiveItem(dateTime)
                {
                    ItemID = item.ID,
                    By = Context.User.Name,
                    DatabaseName = item.Database.Name,
                    ArchiveName = "archive"
                };
                if (dateTime > DateTime.MinValue)
                {
                    archiveItem.Update();
                }
                else
                {
                    archiveItem.Remove();
                }
            }
            DateField dateField2 = item.Fields[FieldIDs.ArchiveVersionDate];
            if (force || dateField2.InnerField.IsModified)
            {
                DateTime dateTime2 = dateField2.DateTime;
                ArchiveVersion archiveVersion = new ArchiveVersion(dateTime2)
                {
                    ItemID = item.ID,
                    DatabaseName = item.Database.Name,
                    By = Context.User.Name,
                    Language = item.Language.Name,
                    Version = item.Version.Number,
                    ArchiveName = "archive"
                };
                if (dateTime2 > DateTime.MinValue)
                {
                    archiveVersion.Update();
                    return;
                }
                archiveVersion.Remove();
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
                if (dateTime > DateTime.MinValue)
                {
                    EmailReminderTask emailReminderTask = new EmailReminderTask(dateTime);
                    emailReminderTask.ItemID = item.ID;
                    emailReminderTask.DatabaseName = item.Database.Name;
                    emailReminderTask.Parameters = this.GetReminderParameters(field, field2);
                    Globals.TaskDatabase.UpdateItemTask(emailReminderTask, true);
                    return;
                }
                Globals.TaskDatabase.RemoveItemTasks(item, typeof(EmailReminderTask));
            }
        }
    }
}