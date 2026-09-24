using System.Globalization;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria;

/// <summary>
/// The platform seed for M0-D02: 2017 Trust Services Criteria identifiers (2022 revised
/// points of focus edition) with original Bdgrz summaries. No AICPA text is stored.
/// </summary>
static class PlatformCriteriaCatalog
{
    static readonly Uuid EditionId = Uuid.Parse("0f3c5a52-9d8e-5b4a-8f21-20172022a0f1",
        CultureInfo.InvariantCulture);
    const string SourceUrl =
        "https://www.aicpa-cima.com/resources/download/2017-trust-services-criteria-with-revised-points-of-focus-2022";
    const string FocusGap =
        "Only selected points of focus are mapped, under local bdgrz:focus identifiers; the source does not number them.";

    public static CriteriaCatalog Create() => new(
        new CriteriaCatalogEdition(EditionId, "tsc", "2017_tsc_2022_pof",
            new DateTimeOffset(2026, 9, 24, 0, 0, 0, TimeSpan.Zero), true,
            "All 61 numbered 2017 criteria with original summaries. Points of focus are partially mapped.",
            SourceUrl, "identifiers_and_original_summaries",
            [
                new("security", "points_of_focus_partial", FocusGap),
                new("availability", "points_of_focus_partial", FocusGap),
                new("confidentiality", "points_of_focus_partial", FocusGap),
                new("processing_integrity", "points_of_focus_partial", FocusGap),
                new("privacy", "points_of_focus_partial", FocusGap),
                new("privacy", "privacy_lifecycle_unsupported",
                    "Privacy criteria may be mapped to controls, but personal-information lifecycle support is not yet available (#349)."),
            ]),
        [
            C("CC1.1", "security", "Set and uphold standards of integrity and ethical conduct across the organization."),
            C("CC1.2", "security", "A governing body oversees internal control independently of management."),
            C("CC1.3", "security", "Management defines structures, reporting lines, and authority to pursue objectives."),
            C("CC1.4", "security", "Attract, develop, and retain competent people aligned with objectives."),
            C("CC1.5", "security", "Hold individuals accountable for their internal control responsibilities."),
            C("CC2.1", "security", "Gather and use relevant, quality information to support internal control."),
            C("CC2.2", "security", "Share internal control objectives and responsibilities with the workforce."),
            C("CC2.3", "security", "Communicate with outside parties about matters affecting internal control."),
            C("CC3.1", "security", "State objectives clearly enough to identify and assess related risks."),
            C("CC3.2", "security", "Identify and analyze risks to objectives to decide how to manage them."),
            C("CC3.3", "security", "Consider fraud potential when assessing risks to objectives."),
            C("CC3.4", "security", "Identify and assess changes that could significantly affect internal control."),
            C("CC4.1", "security", "Run ongoing or separate evaluations to confirm controls are present and working."),
            C("CC4.2", "security", "Evaluate deficiencies and report them promptly to those who can act."),
            C("CC5.1", "security", "Choose and build control activities that reduce risks to acceptable levels."),
            C("CC5.2", "security", "Choose and build general technology controls that support objectives."),
            C("CC5.3", "security", "Put control activities into practice through policies and procedures."),
            C("CC6.1", "security", "Protect system information and technology with controlled logical access."),
            C("CC6.2", "security", "Register and authorize users before issuing credentials, and revoke them when unneeded."),
            C("CC6.3", "security", "Grant, change, and revoke access by role, least privilege, and segregation of duties."),
            C("CC6.4", "security", "Restrict physical access to facilities and protected assets to authorized people."),
            C("CC6.5", "security", "Dispose of physical assets only once the data on them cannot be recovered."),
            C("CC6.6", "security", "Guard system boundaries against threats originating outside them."),
            C("CC6.7", "security", "Limit and protect the movement of information to authorized users and processes."),
            C("CC6.8", "security", "Prevent or detect unauthorized or malicious software."),
            C("CC7.1", "security", "Detect configuration changes and new vulnerabilities through monitoring procedures."),
            C("CC7.2", "security", "Watch system components for anomalies that may signal attacks, errors, or failures."),
            C("CC7.3", "security", "Evaluate security events to decide whether they are incidents needing a response."),
            C("CC7.4", "security", "Respond to identified security incidents with a defined program."),
            C("CC7.5", "security", "Recover from identified security incidents and restore normal operations."),
            C("CC8.1", "security", "Control system changes from authorization through testing, approval, and deployment."),
            C("CC9.1", "security", "Identify and plan for risks from potential business disruptions."),
            C("CC9.2", "security", "Assess and manage risks arising from vendors and business partners."),
            C("A1.1", "availability", "Track processing capacity against availability commitments."),
            C("A1.2", "availability", "Protect, back up, and recover the environmental and infrastructure components availability relies on."),
            C("A1.3", "availability", "Test the procedures that support system recovery."),
            C("C1.1", "confidentiality", "Identify confidential information and apply its handling rules."),
            C("C1.2", "confidentiality", "Dispose of confidential information when its retention requirements end."),
            C("PI1.1", "processing_integrity", "Define information quality needs for reliable processing."),
            C("PI1.2", "processing_integrity", "Validate system inputs for completeness and accuracy."),
            C("PI1.3", "processing_integrity", "Process data completely, accurately, and on time against specifications."),
            C("PI1.4", "processing_integrity", "Deliver outputs completely and accurately only to intended recipients."),
            C("PI1.5", "processing_integrity", "Store inputs, in-process items, and outputs completely, accurately, and on time."),
            C("P1.1", "privacy", "Explain privacy practices to the people whose information is processed."),
            C("P2.1", "privacy", "Offer choices and obtain consent for how personal information is collected and used."),
            C("P3.1", "privacy", "Collect personal information only as needed for stated privacy objectives."),
            C("P3.2", "privacy", "Obtain explicit consent where required before collecting personal information."),
            C("P4.1", "privacy", "Limit use of personal information to the purposes identified."),
            C("P4.2", "privacy", "Keep personal information only as long as it is needed."),
            C("P4.3", "privacy", "Securely dispose of personal information at the end of retention."),
            C("P5.1", "privacy", "Let data subjects access their personal information on request."),
            C("P5.2", "privacy", "Correct, amend, or append personal information when data subjects ask."),
            C("P6.1", "privacy", "Disclose personal information to third parties only with consent or a permitted purpose."),
            C("P6.2", "privacy", "Keep complete, accurate records of authorized disclosures of personal information."),
            C("P6.3", "privacy", "Record detected or reported unauthorized disclosures, including breaches."),
            C("P6.4", "privacy", "Obtain privacy commitments from vendors and others who handle personal information."),
            C("P6.5", "privacy", "Require vendors to report unauthorized disclosures of personal information."),
            C("P6.6", "privacy", "Notify affected parties of breaches and incidents as commitments require."),
            C("P6.7", "privacy", "Give data subjects an account of personal information held and shared about them."),
            C("P7.1", "privacy", "Keep personal information accurate, complete, and relevant."),
            C("P8.1", "privacy", "Handle privacy inquiries, complaints, and disputes, and monitor compliance."),
            F("cc6-1:asset-inventory", "security", "CC6.1",
                "Maintain a classified inventory of information and technology assets."),
            F("cc6-2:access-removal", "security", "CC6.2",
                "Remove credentials promptly once a user no longer requires access."),
            F("a1-2:backup-restoration", "availability", "A1.2",
                "Back up data and confirm that backups can be restored."),
            F("c1-2:disposal-verification", "confidentiality", "C1.2",
                "Confirm that confidential information marked for disposal is actually destroyed."),
            F("pi1-2:input-edits", "processing_integrity", "PI1.2",
                "Apply edit checks that reject incomplete or invalid inputs."),
            F("p4-2:retention-schedule", "privacy", "P4.2",
                "Follow a documented retention schedule for personal information."),
        ]);

    static Criterion C(string identifier, string category, string summary) =>
        new(EditionId, identifier, identifier, category, "criterion", null, summary);

    static Criterion F(string localKey, string category, string parent, string summary) =>
        new(EditionId, "bdgrz:focus:" + localKey, null, category, "point_of_focus", parent,
            summary);
}
