using System.Globalization;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

static class ApplicationImportDirectorySchema
{
    public static readonly KvDirectory<ApplicationImportView, Uuid> Batches = new(
        "application_import_batches", ComplianceCoreJsonContext.Default.ApplicationImportView,
        static batch => batch.BatchId, static id => [id.ToString()], []);

    public static readonly KvDirectoryIndex<ApplicationImportRowView> ByBatchOrdinal = new(
        "by_batch_ordinal", 1, static row =>
            [row.BatchId.ToString(), row.RowNumber.ToString("D4", CultureInfo.InvariantCulture)]);

    public static readonly KvDirectory<ApplicationImportRowView, Uuid> Rows = new(
        "application_import_rows", ComplianceCoreJsonContext.Default.ApplicationImportRowView,
        static row => row.RowId, static id => [id.ToString()], [ByBatchOrdinal]);
}
