using HAMBOX.Modules.Legal.Domain.Legal;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HAMBOX.Modules.Legal.Infrastructure.Persistence;

/// <summary>
/// Seeds and publishes starter content for the four legal sections the checkout/registration
/// acceptance flow requires (ADM-44/45): Terms &amp; Conditions, Privacy Policy, Refund Policy, and
/// Digital Delivery Policy. Distinct from <see cref="LegalDataSeeder"/>, which creates all default
/// sections as empty, unpublished drafts (by design, for an admin to write from scratch) — this
/// seeder only touches the four <see cref="RequiredSlugs"/> and always leaves them with a real,
/// published version, since <c>LegalAcceptanceRecorder</c> only records acceptance for sections that
/// have one. It never overwrites a slug that already has a substantive published version, so an
/// admin's own edits are never clobbered by a re-run — <see cref="KnownPlaceholderContent"/> is a
/// narrow, exact-match exception for two specific one-word stub values found already published in
/// the local dev database at the time this seeder was written ("Privacy" / "refund" test content,
/// not real policy text); anything else published is left alone.
/// </summary>
public static class LegalRequiredContentSeeder
{
    private static readonly string[] RequiredSlugs = ["terms", "privacy", "refund", "delivery"];

    private static readonly HashSet<string> KnownPlaceholderContent = new(StringComparer.OrdinalIgnoreCase)
    {
        "<p>Privacy</p>",
        "<p>refund</p>",
    };

    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LegalDbContext>();

        var existing = await db.LegalSections
            .Include(s => s.Versions)
            .Where(s => RequiredSlugs.Contains(s.Slug))
            .ToDictionaryAsync(s => s.Slug);

        foreach (var (slug, titleEn, contentEn, category, icon) in Content.All)
        {
            if (existing.TryGetValue(slug, out var section))
            {
                // A pre-existing row can predate the fix (documented in this audit's own Rev.4
                // notes) that flipped the Digital Delivery Policy's seed default from false to
                // true — correct it here too, independent of content/publish status, since a
                // stale false silently excludes the section from LegalAcceptanceRecorder entirely.
                if (!section.RequireAcceptance)
                {
                    section.UpdateMetadata(
                        section.Slug,
                        section.Category,
                        section.Icon,
                        section.SortOrder,
                        section.DescriptionEn,
                        section.DescriptionAr,
                        section.SeoTitle,
                        section.SeoDescription,
                        section.SeoKeywords,
                        section.ShowInFooter,
                        section.ShowInNavigation,
                        requireAcceptance: true);
                }

                var publishedVersion = section.Versions.FirstOrDefault(v => v.Id == section.PublishedVersionId);
                var isKnownPlaceholder = publishedVersion is not null && KnownPlaceholderContent.Contains(publishedVersion.ContentEn.Trim());

                if (section.PublishedVersionId is not null && !isKnownPlaceholder)
                {
                    continue;
                }

                if (isKnownPlaceholder)
                {
                    // Published versions are immutable (LegalSectionVersion.UpdateContent throws) —
                    // a new version is the only way to replace stub content, exactly what an admin
                    // publishing an edit through the UI would produce. The one-word v1 stays in
                    // LegalSectionVersions/history, it just stops being the live one.
                    var replacement = section.CreateDraftVersion(
                        titleEn, null, contentEn, null, "Replaces placeholder test content with initial real policy text.");
                    db.LegalSectionVersions.Add(replacement);
                    section.PublishVersion(replacement.Id, publishedBy: "system-seed");
                    continue;
                }

                var draft = section.GetCurrentDraft();
                if (draft is null)
                {
                    draft = section.CreateDraftVersion(titleEn, null, contentEn, null, "Initial published version.");
                    db.LegalSectionVersions.Add(draft);
                }

                draft.UpdateContent(titleEn, null, contentEn, null, "Initial published version.");
                section.PublishVersion(draft.Id, publishedBy: "system-seed");
                continue;
            }

            section = LegalSection.Create(slug);
            section.UpdateMetadata(
                slug,
                category,
                icon,
                sortOrder: 0,
                descriptionEn: null,
                descriptionAr: null,
                seoTitle: null,
                seoDescription: null,
                seoKeywords: null,
                showInFooter: true,
                showInNavigation: true,
                requireAcceptance: true);
            var version = section.CreateDraftVersion(titleEn, null, contentEn, null, "Initial published version.");
            db.LegalSections.Add(section);
            section.PublishVersion(version.Id, publishedBy: "system-seed");
        }

        await db.SaveChangesAsync();
    }

    private static class Content
    {
        public static readonly (string Slug, string TitleEn, string ContentEn, string Category, string Icon)[] All =
        [
            ("terms", "Terms & Conditions", Terms, "Legal", "pi pi-file-check"),
            ("privacy", "Privacy Policy", Privacy, "Legal", "pi pi-shield"),
            ("refund", "Refund Policy", Refund, "Legal", "pi pi-replay"),
            ("delivery", "Digital Delivery Policy", Delivery, "Commerce", "pi pi-bolt"),
        ];

        public const string Terms = """
            <h2>1. About HAMBOX</h2>
            <p>HAMBOX ("we", "us", "the platform") is a digital-goods marketplace. We sell game keys, gift cards,
            subscriptions, and digital account top-ups. Every product sold through HAMBOX is delivered
            electronically — we do not sell or ship physical goods.</p>

            <h2>2. Accounts</h2>
            <p>You must create an account and verify your email address before completing a purchase. You are
            responsible for keeping your account credentials confidential and for all activity that occurs
            under your account. We may suspend or restrict an account we reasonably believe is being used
            fraudulently or abusively.</p>

            <h2>3. Orders and Pricing</h2>
            <p>Prices are displayed on the storefront in USD, EUR, EGP, and SAR for your convenience; the
            currency you see is a display conversion only, and USD is the currency your order is recorded and
            settled in. Placing an order constitutes an offer to purchase, which we accept once payment is
            confirmed by our payment provider.</p>

            <h2>4. Payment</h2>
            <p>Payments are processed through our third-party payment gateway. We do not collect or store your
            full card details on our servers — payment is completed on the gateway's own secure, hosted
            checkout page. Depending on your location and the payment method you choose, this may include
            card payments or supported local wallet options.</p>

            <h2>5. Digital Delivery</h2>
            <p>All products are delivered digitally to your HAMBOX account library after payment is confirmed.
            See our <strong>Digital Delivery Policy</strong> for details on delivery timing and what happens if
            delivery is delayed.</p>

            <h2>6. Refunds</h2>
            <p>Refund eligibility for digital goods is limited, given that codes and keys cannot be "returned"
            once revealed. See our <strong>Refund Policy</strong> for the specific circumstances in which a
            refund may be issued.</p>

            <h2>7. Referral Program</h2>
            <p>HAMBOX may offer a referral program under which you can earn points for inviting other
            customers, subject to the program's own rules as published in your account dashboard at the time.
            We reserve the right to withhold or reverse referral points obtained through abuse of the program
            (for example, self-referral or the use of duplicate accounts).</p>

            <h2>8. Acceptable Use</h2>
            <p>You agree not to use HAMBOX for any fraudulent, unlawful, or abusive purpose, including but not
            limited to using stolen payment methods, attempting to circumvent purchase limits, or reselling
            codes obtained through fraudulent chargebacks. We may cancel orders and suspend accounts involved
            in such activity.</p>

            <h2>9. Limitation of Liability</h2>
            <p>To the maximum extent permitted by law, HAMBOX is not liable for indirect or consequential
            losses arising from your use of the platform, including losses caused by a third-party supplier's
            or payment provider's failure to perform.</p>

            <h2>10. Changes to These Terms</h2>
            <p>We may update these Terms from time to time. Where a section requires your acceptance, you will
            be asked to review and accept the current version the next time it applies to you (for example, at
            your next checkout).</p>

            <h2>11. Governing Law</h2>
            <p><em>[Placeholder — governing law and jurisdiction to be confirmed by HAMBOX's legal counsel
            before this document is treated as final.]</em></p>

            <h2>12. Contact</h2>
            <p>Questions about these Terms can be directed to our support team through your account dashboard.</p>
            """;

        public const string Privacy = """
            <h2>1. What This Policy Covers</h2>
            <p>This Privacy Policy describes what information HAMBOX collects when you use our storefront and
            account dashboard, and how we use it. It reflects what our platform actually collects today, not a
            general template.</p>

            <h2>2. Information You Provide</h2>
            <ul>
                <li>Account details: email address, first and last name, and password (stored as a secure hash,
                never in plain text).</li>
                <li>Optional profile details: phone number and avatar image, if you choose to add them.</li>
                <li>Order details: the email and country associated with each order, and the payment method
                type you select.</li>
                <li>Support and review content: anything you submit through a support ticket or a product
                review.</li>
            </ul>

            <h2>3. Information Collected Automatically</h2>
            <ul>
                <li>Security and audit information: your IP address, browser/device user-agent string, and
                timestamps, recorded against security-relevant events (login, password reset, email
                verification, order actions) to protect your account and detect abuse.</li>
                <li>Preferences: your selected display language and currency, and your light/dark theme choice,
                stored either on your account or in your browser's local storage so the site remembers your
                choice on your next visit.</li>
                <li>Session cookies: a secure, HTTP-only cookie used to keep you signed in, and a corresponding
                anti-forgery (CSRF) cookie used to protect account actions — both are functional/security
                cookies, not advertising cookies.</li>
            </ul>
            <p>At the time of writing, HAMBOX does not run any third-party analytics or advertising tracking
            (such as Google Analytics or Meta Pixel) on the storefront. If this changes in the future, this
            policy will be updated before any such tracking goes live.</p>

            <h2>4. Payment Information</h2>
            <p>We do not store your full card number or card security code. Payment is completed on our
            payment provider's own hosted checkout page, and we only retain the payment method type and the
            provider's transaction reference for your order.</p>

            <h2>5. Digital Codes and License Keys</h2>
            <p>The digital codes and license keys you purchase are stored encrypted at rest and are only ever
            decrypted to display them to you, or to an authorized administrator handling an order issue.</p>

            <h2>6. How We Use Your Information</h2>
            <ul>
                <li>To create and secure your account, and to process and deliver your orders.</li>
                <li>To detect and prevent fraud, account abuse, and unauthorized access.</li>
                <li>To communicate with you about your account and orders (for example, order confirmation and
                delivery emails, and email verification/password reset messages).</li>
                <li>To operate the referral program, if you choose to participate in it.</li>
            </ul>

            <h2>7. Data Sharing</h2>
            <p>We share the minimum information necessary with our payment provider to process your payment,
            and with our digital-goods suppliers to fulfill the product you purchased (for example, the order
            reference needed to source your code). We do not sell your personal information to third parties.</p>

            <h2>8. Data Retention</h2>
            <p><em>[Placeholder — a specific data retention and deletion schedule requires business/legal
            confirmation and will be added here once defined.]</em></p>

            <h2>9. Your Choices</h2>
            <p>You can update your profile details from your account dashboard at any time. To request
            deletion of your account or data, please contact our support team.</p>

            <h2>10. Changes to This Policy</h2>
            <p>We may update this Privacy Policy as our platform evolves. Material changes will be reflected
            in a new published version of this document.</p>
            """;

        public const string Refund = """
            <h2>1. General Policy</h2>
            <p>Because HAMBOX sells digital codes and license keys, most sales are final once a code has been
            revealed or delivered to your account — a revealed digital code cannot be "returned" the way a
            physical product can. This policy explains the specific situations in which a refund is available.</p>

            <h2>2. When a Refund May Be Issued</h2>
            <ul>
                <li><strong>Non-delivery:</strong> if your order is not fulfilled and no valid code is delivered
                to your account within a reasonable time, you are entitled to a refund or a replacement code, at
                our discretion.</li>
                <li><strong>Invalid or defective code:</strong> if the code you received does not work as
                described (for example, it is rejected by the platform it's meant to activate on), contact
                support with the details so we can investigate and, where confirmed, issue a refund or
                replacement.</li>
                <li><strong>Duplicate or erroneous charge:</strong> if you were charged more than once for the
                same order, or charged in error, the duplicate/erroneous amount will be refunded.</li>
                <li><strong>Order cancelled before delivery:</strong> if your order is cancelled before any code
                has been revealed to you, you are entitled to a full refund.</li>
            </ul>

            <h2>3. When a Refund Is Not Available</h2>
            <p>We cannot offer a refund once a code has been successfully delivered and revealed to you, except
            in the situations described in Section 2. This is standard practice for digital goods, since a
            revealed code cannot be verified as unused once it has left our systems.</p>

            <h2>4. How to Request a Refund</h2>
            <p>Contact our support team through your account dashboard with your order number and the reason
            for your request. Refund requests are reviewed individually by our team — there is no automatic,
            instant self-service refund button today, so please allow time for a member of our team to review
            your case.</p>

            <h2>5. How Refunds Are Processed</h2>
            <p>Approved refunds are returned to the original payment method used for the order, through our
            payment provider. Processing time depends on your payment provider and is outside HAMBOX's direct
            control.</p>

            <h2>6. Changes to This Policy</h2>
            <p>We may update this Refund Policy from time to time; the version in effect at the time of your
            order is the one that applies to that order.</p>
            """;

        public const string Delivery = """
            <h2>1. How Delivery Works</h2>
            <p>HAMBOX delivers every product digitally — there is no physical shipment. Once your payment is
            confirmed, our system attempts to fulfill your order automatically, sourcing the code or key for
            the product you purchased and adding it to your account library.</p>

            <h2>2. When You'll Receive Your Code</h2>
            <p>In most cases, your code is available in your account library within a few minutes of payment
            confirmation. Delivery time can vary depending on the product and on the availability of stock at
            the time of your order.</p>

            <h2>3. Where to Find Your Codes</h2>
            <p>All your purchased codes are available under "My Codes" / Library in your account dashboard.
            Each item shows its delivery status (delivered or pending) and lets you reveal, copy, and view
            redemption instructions for your code.</p>

            <h2>4. If Delivery Is Delayed</h2>
            <p>Our system continuously monitors order fulfillment and automatically flags any order that is
            taking longer than expected, so our operations team can review it — you do not need to wait
            indefinitely without anyone being aware of the delay. If your order shows as pending for longer
            than you'd expect, you can also contact our support team directly with your order number.</p>

            <h2>5. If Fulfillment Is Unsuccessful</h2>
            <p>If we're unable to source a valid code for your order, our team will resolve it manually — this
            may mean assigning a code by hand, or cancelling and refunding the order in line with our Refund
            Policy, whichever is appropriate to your situation.</p>

            <h2>6. Order Status</h2>
            <p>Your order moves through the following statuses, visible in your order history: Pending (placed,
            awaiting fulfillment), Processing (payment confirmed, fulfillment in progress), Completed
            (delivered), Cancelled, Refunded, or Failed. Each status reflects the real, current state of your
            order at the time you check it.</p>

            <h2>7. Changes to This Policy</h2>
            <p>We may update this Digital Delivery Policy as our fulfillment process evolves.</p>
            """;
    }
}
