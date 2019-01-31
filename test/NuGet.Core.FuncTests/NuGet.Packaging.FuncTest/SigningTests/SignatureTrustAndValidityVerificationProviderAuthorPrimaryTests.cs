// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Packaging.Signing;
using NuGet.Test.Utility;
using Test.Utility.Signing;
using Xunit;

namespace NuGet.Packaging.FuncTest
{
    [Collection(SigningTestCollection.Name)]
    public class SignatureTrustAndValidityVerificationProviderAuthorPrimaryTests
    {
        private readonly SigningTestFixture _fixture;
        private readonly SignatureTrustAndValidityVerificationProvider _provider;

        public SignatureTrustAndValidityVerificationProviderAuthorPrimaryTests(SigningTestFixture fixture)
        {
            _fixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
            _provider = new SignatureTrustAndValidityVerificationProvider();
        }

        [CIOnlyFact]
        public async Task GetTrustResultAsync_WithRepositorySignedPackage_ReturnsUnknownAsync()
        {
            var settings = new SignedPackageVerifierSettings(
                allowUnsigned: false,
                allowIllegal: false,
                allowUntrusted: false,
                allowIgnoreTimestamp: false,
                allowMultipleTimestamps: false,
                allowNoTimestamp: false,
                allowUnknownRevocation: true,
                reportUnknownRevocation: true,
                verificationTarget: VerificationTarget.Author,
                signaturePlacement: SignaturePlacement.PrimarySignature,
                repositoryCountersignatureVerificationBehavior: SignatureVerificationBehavior.Never,
                revocationMode: RevocationMode.Online);

            using (var test = await AuthorPrimaryTest.CreateRepositoryPrimarySignedPackageAsync(_fixture.TrustedTestCertificate.Source.Cert))
            using (var packageReader = new PackageArchiveReader(test.PackageFile.FullName))
            {
                var provider = new SignatureTrustAndValidityVerificationProvider();
                var primarySignature = await packageReader.GetPrimarySignatureAsync(CancellationToken.None);
                var result = await provider.GetTrustResultAsync(packageReader, primarySignature, settings, CancellationToken.None);

                Assert.Equal(SignatureVerificationStatus.Unknown, result.Trust);
            }
        }
#if SUPPORTS_FULL_SIGNING
        [CIOnlyFact]
        public async Task GetTrustResultAsync_WithValidSignature_Timestamped_ReturnsValidAsync()
        {
            var settings = new SignedPackageVerifierSettings(
                allowUnsigned: false,
                allowIllegal: false,
                allowUntrusted: false,
                allowIgnoreTimestamp: false,
                allowMultipleTimestamps: false,
                allowNoTimestamp: false,
                allowUnknownRevocation: true,
                reportUnknownRevocation: true,
                verificationTarget: VerificationTarget.Author,
                signaturePlacement: SignaturePlacement.PrimarySignature,
                repositoryCountersignatureVerificationBehavior: SignatureVerificationBehavior.Never,
                revocationMode: RevocationMode.Online);
            var timestampService = await _fixture.GetDefaultTrustedTimestampServiceAsync();

            using (var test = await AuthorPrimaryTest.CreateAuthorSignedPackageAsync(
                _fixture.TrustedTestCertificate.Source.Cert,
                timestampService.Url))
            using (var packageReader = new PackageArchiveReader(test.PackageFile.FullName))
            {
                var primarySignature = await packageReader.GetPrimarySignatureAsync(CancellationToken.None);

                var status = await _provider.GetTrustResultAsync(packageReader, primarySignature, settings, CancellationToken.None);

                Assert.Equal(SignatureVerificationStatus.Valid, status.Trust);
            }
        }
#endif
        [CIOnlyFact]
        public async Task GetTrustResultAsync_WithValidSignature_ReturnsValidAsync()
        {
            var settings = new SignedPackageVerifierSettings(
                allowUnsigned: false,
                allowIllegal: false,
                allowUntrusted: false,
                allowIgnoreTimestamp: false,
                allowMultipleTimestamps: false,
                allowNoTimestamp: true,
                allowUnknownRevocation: true,
                reportUnknownRevocation: true,
                verificationTarget: VerificationTarget.Author,
                signaturePlacement: SignaturePlacement.PrimarySignature,
                repositoryCountersignatureVerificationBehavior: SignatureVerificationBehavior.Never,
                revocationMode: RevocationMode.Online);

            using (var test = await AuthorPrimaryTest.CreateAuthorSignedPackageAsync(
                _fixture.TrustedTestCertificate.Source.Cert))
            using (var packageReader = new PackageArchiveReader(test.PackageFile.FullName))
            {
                var primarySignature = await packageReader.GetPrimarySignatureAsync(CancellationToken.None);

                var status = await _provider.GetTrustResultAsync(packageReader, primarySignature, settings, CancellationToken.None);

                Assert.Equal(SignatureVerificationStatus.Valid, status.Trust);
            }
        }

        [CIOnlyTheory]
        [InlineData(true, SignatureVerificationStatus.Valid)]
        [InlineData(false, SignatureVerificationStatus.Disallowed)]
        public async Task GetTrustResultAsync_WithValidSignatureButNoTimestamp_ReturnsStatusAsync(
            bool allowNoTimestamp,
            SignatureVerificationStatus expectedStatus)
        {
            var settings = new SignedPackageVerifierSettings(
                allowUnsigned: false,
                allowIllegal: false,
                allowUntrusted: false,
                allowIgnoreTimestamp: allowNoTimestamp,
                allowMultipleTimestamps: false,
                allowNoTimestamp: allowNoTimestamp,
                allowUnknownRevocation: true,
                reportUnknownRevocation: true,
                verificationTarget: VerificationTarget.Author,
                signaturePlacement: SignaturePlacement.PrimarySignature,
                repositoryCountersignatureVerificationBehavior: SignatureVerificationBehavior.Never,
                revocationMode: RevocationMode.Online);

            using (var test = await AuthorPrimaryTest.CreateAuthorSignedPackageAsync(_fixture.TrustedTestCertificate.Source.Cert))
            using (var packageReader = new PackageArchiveReader(test.PackageFile.FullName))
            {
                var primarySignature = await packageReader.GetPrimarySignatureAsync(CancellationToken.None);

                var status = await _provider.GetTrustResultAsync(packageReader, primarySignature, settings, CancellationToken.None);

                Assert.Equal(expectedStatus, status.Trust);
            }
        }
        [CIOnlyTheory]
        [InlineData(true, SignatureVerificationStatus.Valid)]
        [InlineData(false, SignatureVerificationStatus.Disallowed)]
        public async Task GetTrustResultAsync_WithUntrustedSignature_ReturnsStatusAsync(
            bool allowUntrusted,
            SignatureVerificationStatus expectedStatus)
        {
            var settings = new SignedPackageVerifierSettings(
                allowUnsigned: false,
                allowIllegal: false,
                allowUntrusted: allowUntrusted,
                allowIgnoreTimestamp: false,
                allowMultipleTimestamps: false,
                allowNoTimestamp: true,
                allowUnknownRevocation: true,
                reportUnknownRevocation: true,
                verificationTarget: VerificationTarget.Author,
                signaturePlacement: SignaturePlacement.PrimarySignature,
                repositoryCountersignatureVerificationBehavior: SignatureVerificationBehavior.Never,
                revocationMode: RevocationMode.Online);

            using (var test = await AuthorPrimaryTest.CreateAuthorSignedPackageAsync(
                _fixture.UntrustedTestCertificate.Cert))
            using (var packageReader = new PackageArchiveReader(test.PackageFile.FullName))
            {
                var primarySignature = await packageReader.GetPrimarySignatureAsync(CancellationToken.None);

                var status = await _provider.GetTrustResultAsync(packageReader, primarySignature, settings, CancellationToken.None);

                Assert.Equal(expectedStatus, status.Trust);
            }
        }

#if SUPPORTS_FULL_SIGNING
        [CIOnlyTheory]
        [InlineData(true, SignatureVerificationStatus.Valid)]
        [InlineData(false, SignatureVerificationStatus.Disallowed)]
        public async Task GetTrustResultAsync_WithUntrustedSignature_Timestamped_ReturnsStatusAsync(
            bool allowUntrusted,
            SignatureVerificationStatus expectedStatus)
        {
            var settings = new SignedPackageVerifierSettings(
                allowUnsigned: false,
                allowIllegal: false,
                allowUntrusted: allowUntrusted,
                allowIgnoreTimestamp: false,
                allowMultipleTimestamps: false,
                allowNoTimestamp: false,
                allowUnknownRevocation: true,
                reportUnknownRevocation: true,
                verificationTarget: VerificationTarget.Author,
                signaturePlacement: SignaturePlacement.PrimarySignature,
                repositoryCountersignatureVerificationBehavior: SignatureVerificationBehavior.Never,
                revocationMode: RevocationMode.Online);
            var timestampService = await _fixture.GetDefaultTrustedTimestampServiceAsync();

            using (var test = await AuthorPrimaryTest.CreateAuthorSignedPackageAsync(
                _fixture.UntrustedTestCertificate.Cert,
                timestampService.Url))
            using (var packageReader = new PackageArchiveReader(test.PackageFile.FullName))
            {
                var primarySignature = await packageReader.GetPrimarySignatureAsync(CancellationToken.None);

                var status = await _provider.GetTrustResultAsync(packageReader, primarySignature, settings, CancellationToken.None);

                Assert.Equal(expectedStatus, status.Trust);
            }
        }

        // OCSP are not supported on Linux
        [CIOnlyPlatformTheory(Platform.Windows, Platform.Darwin)]
        [InlineData(true)]
        [InlineData(false)]
        public async Task GetTrustResultAsync_WithRevokedPrimaryCertificate_Timestamped_ReturnsSuspectAsync(bool allowEverything)
        {
            var settings = new SignedPackageVerifierSettings(
                allowUnsigned: allowEverything,
                allowIllegal: allowEverything,
                allowUntrusted: allowEverything,
                allowIgnoreTimestamp: allowEverything,
                allowMultipleTimestamps: allowEverything,
                allowNoTimestamp: allowEverything,
                allowUnknownRevocation: true,
                reportUnknownRevocation: true,
                verificationTarget: VerificationTarget.Author,
                signaturePlacement: SignaturePlacement.PrimarySignature,
                repositoryCountersignatureVerificationBehavior: SignatureVerificationBehavior.Never,
                revocationMode: RevocationMode.Online);
            var testServer = await _fixture.GetSigningTestServerAsync();
            var certificateAuthority = await _fixture.GetDefaultTrustedCertificateAuthorityAsync();
            var issueCertificateOptions = IssueCertificateOptions.CreateDefaultForEndCertificate();
            var timestampService = await _fixture.GetDefaultTrustedTimestampServiceAsync();

            using (var certificate = certificateAuthority.IssueCertificate(issueCertificateOptions))
            using (var test = await AuthorPrimaryTest.CreateAuthorSignedPackageAsync(
                certificate,
                timestampService.Url))
            using (var packageReader = new PackageArchiveReader(test.PackageFile.FullName))
            {
                await certificateAuthority.OcspResponder.WaitForResponseExpirationAsync(certificate);

                certificateAuthority.Revoke(
                    certificate,
                    RevocationReason.KeyCompromise,
                    DateTimeOffset.UtcNow.AddHours(-1));

                var primarySignature = await packageReader.GetPrimarySignatureAsync(CancellationToken.None);

                var status = await _provider.GetTrustResultAsync(packageReader, primarySignature, settings, CancellationToken.None);

                Assert.Equal(SignatureVerificationStatus.Suspect, status.Trust);
            }
        }

        // OCSP are not supported on Linux
        [CIOnlyPlatformTheory(Platform.Windows, Platform.Darwin)]
        [InlineData(true, SignatureVerificationStatus.Valid)]
        [InlineData(false, SignatureVerificationStatus.Disallowed)]
        public async Task GetTrustResultAsync_WithRevokedTimestampCertificate_ReturnsStatusAsync(
            bool allowIgnoreTimestamp,
            SignatureVerificationStatus expectedStatus)
        {
            var settings = new SignedPackageVerifierSettings(
                allowUnsigned: false,
                allowIllegal: false,
                allowUntrusted: false,
                allowIgnoreTimestamp: allowIgnoreTimestamp,
                allowMultipleTimestamps: false,
                allowNoTimestamp: false,
                allowUnknownRevocation: true,
                reportUnknownRevocation: true,
                verificationTarget: VerificationTarget.Author,
                signaturePlacement: SignaturePlacement.PrimarySignature,
                repositoryCountersignatureVerificationBehavior: SignatureVerificationBehavior.Never,
                revocationMode: RevocationMode.Online);
            var testServer = await _fixture.GetSigningTestServerAsync();
            var certificateAuthority = await _fixture.GetDefaultTrustedCertificateAuthorityAsync();
            var timestampService = TimestampService.Create(certificateAuthority);

            using (testServer.RegisterResponder(timestampService))
            using (var test = await AuthorPrimaryTest.CreateAuthorSignedPackageAsync(
                _fixture.TrustedRepositoryCertificate.Source.Cert,
                timestampService.Url))
            using (var packageReader = new PackageArchiveReader(test.PackageFile.FullName))
            {
                await certificateAuthority.OcspResponder.WaitForResponseExpirationAsync(timestampService.Certificate);

                certificateAuthority.Revoke(
                    timestampService.Certificate,
                    RevocationReason.KeyCompromise,
                    DateTimeOffset.UtcNow.AddHours(-1));

                var primarySignature = await packageReader.GetPrimarySignatureAsync(CancellationToken.None);

                var status = await _provider.GetTrustResultAsync(packageReader, primarySignature, settings, CancellationToken.None);

                Assert.Equal(expectedStatus, status.Trust);
            }
        }

        [CIOnlyFact]
        public async Task GetTrustResultAsync_WithTamperedRepositoryPrimarySignedPackage_Timestamped_ReturnsValidAsync()
        {
            var settings = new SignedPackageVerifierSettings(
                allowUnsigned: false,
                allowIllegal: false,
                allowUntrusted: false,
                allowIgnoreTimestamp: false,
                allowMultipleTimestamps: false,
                allowNoTimestamp: false,
                allowUnknownRevocation: true,
                reportUnknownRevocation: true,
                verificationTarget: VerificationTarget.Author,
                signaturePlacement: SignaturePlacement.PrimarySignature,
                repositoryCountersignatureVerificationBehavior: SignatureVerificationBehavior.Never,
                revocationMode: RevocationMode.Online);
            var timestampService = await _fixture.GetDefaultTrustedTimestampServiceAsync();

            using (var test = await AuthorPrimaryTest.CreateAuthorSignedPackageAsync(
                _fixture.TrustedTestCertificate.Source.Cert,
                timestampService.Url))
            {
                using (var stream = test.PackageFile.Open(FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    stream.Position = 0;

                    stream.WriteByte(0x00);
                }

                using (var packageReader = new PackageArchiveReader(test.PackageFile.FullName))
                {
                    var primarySignature = await packageReader.GetPrimarySignatureAsync(CancellationToken.None);

                    var status = await _provider.GetTrustResultAsync(packageReader, primarySignature, settings, CancellationToken.None);

                    Assert.Equal(SignatureVerificationStatus.Valid, status.Trust);
                }
            }
        }
#endif
        [CIOnlyFact]
        public async Task GetTrustResultAsync_WithTamperedRepositoryPrimarySignedPackage_ReturnsValidAsync()
        {
            var settings = new SignedPackageVerifierSettings(
                allowUnsigned: false,
                allowIllegal: false,
                allowUntrusted: false,
                allowIgnoreTimestamp: false,
                allowMultipleTimestamps: false,
                allowNoTimestamp: true,
                allowUnknownRevocation: true,
                reportUnknownRevocation: true,
                verificationTarget: VerificationTarget.Author,
                signaturePlacement: SignaturePlacement.PrimarySignature,
                repositoryCountersignatureVerificationBehavior: SignatureVerificationBehavior.Never,
                revocationMode: RevocationMode.Online);

            using (var test = await AuthorPrimaryTest.CreateAuthorSignedPackageAsync(
                _fixture.TrustedTestCertificate.Source.Cert))
            {
                using (var stream = test.PackageFile.Open(FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    stream.Position = 0;

                    stream.WriteByte(0x00);
                }

                using (var packageReader = new PackageArchiveReader(test.PackageFile.FullName))
                {
                    var primarySignature = await packageReader.GetPrimarySignatureAsync(CancellationToken.None);

                    var status = await _provider.GetTrustResultAsync(packageReader, primarySignature, settings, CancellationToken.None);

                    Assert.Equal(SignatureVerificationStatus.Valid, status.Trust);
                }
            }
        }

        private sealed class AuthorPrimaryTest : IDisposable
        {
            private readonly TestDirectory _directory;
            private bool _isDisposed;

            internal FileInfo PackageFile { get; }

            private AuthorPrimaryTest(TestDirectory directory, FileInfo package)
            {
                _directory = directory;
                PackageFile = package;
            }

            public void Dispose()
            {
                if (!_isDisposed)
                {
                    _directory.Dispose();

                    GC.SuppressFinalize(this);

                    _isDisposed = true;
                }
            }

            internal static async Task<AuthorPrimaryTest> CreateAuthorSignedPackageAsync(
                X509Certificate2 certificate,
                Uri timestampServiceUrl = null)
            {
                var packageContext = new SimpleTestPackageContext();
                var directory = TestDirectory.Create();
                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(
                    certificate,
                    packageContext,
                    directory,
                    timestampServiceUrl);

                return new AuthorPrimaryTest(directory, new FileInfo(signedPackagePath));
            }

            internal static async Task<AuthorPrimaryTest> CreateRepositoryPrimarySignedPackageAsync(
                X509Certificate2 certificate,
                Uri timestampServiceUrl = null)
            {
                var packageContext = new SimpleTestPackageContext();
                var directory = TestDirectory.Create();
                var signedPackagePath = await SignedArchiveTestUtility.RepositorySignPackageAsync(
                    certificate,
                    packageContext,
                    directory,
                    new Uri("https://nuget.test"),
                    timestampServiceUrl);

                return new AuthorPrimaryTest(directory, new FileInfo(signedPackagePath));
            }
        }
    }
}
