// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Packaging.Signing;
using NuGet.Test.Utility;
using Test.Utility.Signing;

namespace NuGet.Packaging.FuncTest
{
    /// <summary>
    /// Used to bootstrap functional tests for signing.
    /// </summary>
    public class SigningTestFixture : IDisposable
    {
        private TrustedTestCert<TestCertificate> _trustedTestCert;
        private TrustedTestCert<TestCertificate> _trustedRepositoryCertificate;
        private TrustedTestCert<TestCertificate> _trustedTestCertExpired;
        private TrustedTestCert<TestCertificate> _trustedTestCertNotYetValid;
        private TrustedTestCert<X509Certificate2> _trustedServerRoot;
        private TestCertificate _untrustedTestCert;
        private IReadOnlyList<TrustedTestCert<TestCertificate>> _trustedTestCertificateWithReissuedCertificate;
        private IList<ISignatureVerificationProvider> _trustProviders;
        private SigningSpecifications _signingSpecifications;
        private Lazy<Task<SigningTestServer>> _testServer;
        private Lazy<Task<CertificateAuthority>> _defaultTrustedCertificateAuthority;
        private Lazy<Task<TimestampService>> _defaultTrustedTimestampService;
        private readonly DisposableList<IDisposable> _responders;
        private readonly TestDirectory _certDir;

        public SigningTestFixture()
        {
            _testServer = new Lazy<Task<SigningTestServer>>(SigningTestServer.CreateAsync);
            _defaultTrustedCertificateAuthority = new Lazy<Task<CertificateAuthority>>(CreateDefaultTrustedCertificateAuthorityAsync);
            _defaultTrustedTimestampService = new Lazy<Task<TimestampService>>(CreateDefaultTrustedTimestampServiceAsync);
            _responders = new DisposableList<IDisposable>();
            _certDir = TestDirectory.Create();
        }

        public TestDirectory CertificatesDirectory => _certDir;

        public TrustedTestCert<TestCertificate> TrustedTestCertificate
        {
            get
            {
                if (_trustedTestCert == null)
                {
                    _trustedTestCert = SigningTestUtility.GenerateTrustedTestCertificate(_certDir);
                }

                return _trustedTestCert;
            }
        }

        // This certificate is interchangeable with TrustedTestCertificate and exists only
        // to provide certificate independence in author + repository signing scenarios.
        public TrustedTestCert<TestCertificate> TrustedRepositoryCertificate
        {
            get
            {
                if (_trustedRepositoryCertificate == null)
                {
                    _trustedRepositoryCertificate = SigningTestUtility.GenerateTrustedTestCertificate(_certDir);
                }

                return _trustedRepositoryCertificate;
            }
        }

        public TrustedTestCert<TestCertificate> TrustedTestCertificateExpired
        {
            get
            {
                if (_trustedTestCertExpired == null)
                {
                    _trustedTestCertExpired = SigningTestUtility.GenerateTrustedTestCertificateExpired(_certDir);
                }

                return _trustedTestCertExpired;
            }
        }

        public TrustedTestCert<TestCertificate> TrustedTestCertificateNotYetValid
        {
            get
            {
                if (_trustedTestCertNotYetValid == null)
                {
                    _trustedTestCertNotYetValid = SigningTestUtility.GenerateTrustedTestCertificateNotYetValid(_certDir);
                }

                return _trustedTestCertNotYetValid;
            }
        }

        // We should not memoize this call because it is a time-sensitive operation.
        public TrustedTestCert<TestCertificate> TrustedTestCertificateWillExpireIn10Seconds => SigningTestUtility.GenerateTrustedTestCertificateThatExpiresIn10Seconds(_certDir);

        // We should not memoize this call because it is a time-sensitive operation.
        public TestCertificate UntrustedTestCertificateWillExpireIn10Seconds => TestCertificate.Generate(SigningTestUtility.CertificateModificationGeneratorExpireIn10Seconds);

        public IReadOnlyList<TrustedTestCert<TestCertificate>> TrustedTestCertificateWithReissuedCertificate
        {
            get
            {
                if (_trustedTestCertificateWithReissuedCertificate == null)
                {
                    using (var rsa = RSA.Create(keySizeInBits: 2048))
                    {
                        var certificateName = TestCertificate.GenerateCertificateName();
                        var certificate1 = SigningTestUtility.GenerateCertificate(certificateName, rsa);
                        var certificate2 = SigningTestUtility.GenerateCertificate(certificateName, rsa);

                        var temp1 = new TestCertificate() { Cert = certificate1 };
                        var temp2 = new TestCertificate() { Cert = certificate2 };
                        
                        TrustedTestCert<TestCertificate> testCertificate1 = null;
                        TrustedTestCert<TestCertificate> testCertificate2 = null;
                        if (RuntimeEnvironmentHelper.IsWindows)
                        {
                            testCertificate1 = temp1.WithTrust(StoreName.Root, StoreLocation.LocalMachine, _certDir);
                            testCertificate2 = temp2.WithTrust(StoreName.Root, StoreLocation.LocalMachine, _certDir);
                        }
                        else if (RuntimeEnvironmentHelper.IsLinux)
                        {
                            testCertificate1 = temp1.WithTrust(StoreName.Root, StoreLocation.CurrentUser, _certDir, trustInLinux: true);
                            testCertificate2 = temp2.WithTrust(StoreName.Root, StoreLocation.CurrentUser, _certDir, trustInLinux: true);
                        }
                        else
                        {
                            testCertificate1 = temp1.WithTrust(StoreName.My, StoreLocation.CurrentUser, _certDir, trustInMac: true);
                            testCertificate2 = temp2.WithTrust(StoreName.My, StoreLocation.CurrentUser, _certDir, trustInMac: true);
                        }

                        _trustedTestCertificateWithReissuedCertificate = new[]
                        {
                            testCertificate1,
                            testCertificate2
                        };
                    }
                }

                return _trustedTestCertificateWithReissuedCertificate;
            }
        }

        public TestCertificate UntrustedTestCertificate
        {
            get
            {
                if (_untrustedTestCert == null)
                {
                    _untrustedTestCert = TestCertificate.Generate(SigningTestUtility.CertificateModificationGeneratorForCodeSigningEkuCert);
                }

                return _untrustedTestCert;
            }
        }

        public IList<ISignatureVerificationProvider> TrustProviders
        {
            get
            {
                if (_trustProviders == null)
                {
                    _trustProviders = new List<ISignatureVerificationProvider>()
                    {
                        new SignatureTrustAndValidityVerificationProvider(),
                        new IntegrityVerificationProvider()
                    };
                }

                return _trustProviders;
            }
        }

        public SigningSpecifications SigningSpecifications
        {
            get
            {
                if (_signingSpecifications == null)
                {
                    _signingSpecifications = SigningSpecifications.V1;
                }

                return _signingSpecifications;
            }
        }

        public async Task<ISigningTestServer> GetSigningTestServerAsync()
        {
            return await _testServer.Value;
        }

        public async Task<CertificateAuthority> GetDefaultTrustedCertificateAuthorityAsync()
        {
            return await _defaultTrustedCertificateAuthority.Value;
        }

        public async Task<TimestampService> GetDefaultTrustedTimestampServiceAsync()
        {
            return await _defaultTrustedTimestampService.Value;
        }

        private async Task<CertificateAuthority> CreateDefaultTrustedCertificateAuthorityAsync()
        {
            var testServer = await _testServer.Value;
            var rootCa = CertificateAuthority.Create(testServer.Url);
            var intermediateCa = rootCa.CreateIntermediateCertificateAuthority();
            var rootCertificate = new X509Certificate2(rootCa.Certificate.GetEncoded());

            if (RuntimeEnvironmentHelper.IsWindows)
            {
                _trustedServerRoot = TrustedTestCert.Create(
                    rootCertificate,
                    StoreName.Root,
                    StoreLocation.LocalMachine,
                    _certDir);
            }
            else if (RuntimeEnvironmentHelper.IsLinux)
            {
                _trustedServerRoot = TrustedTestCert.Create(
                    rootCertificate,
                    StoreName.Root,
                    StoreLocation.CurrentUser,
                    _certDir,
                    trustInLinux: true);
            }
            else
            {
                _trustedServerRoot = TrustedTestCert.Create(
                    rootCertificate,
                    StoreName.My,
                    StoreLocation.CurrentUser,
                    _certDir,
                    trustInMac: true);
            }

            var ca = intermediateCa;

            while (ca != null)
            {
                _responders.Add(testServer.RegisterResponder(ca));
                _responders.Add(testServer.RegisterResponder(ca.OcspResponder));

                ca = ca.Parent;
            }

            return intermediateCa;
        }

        private async Task<TimestampService> CreateDefaultTrustedTimestampServiceAsync()
        {
            var testServer = await _testServer.Value;
            var ca = await _defaultTrustedCertificateAuthority.Value;
            var timestampService = TimestampService.Create(ca);

            _responders.Add(testServer.RegisterResponder(timestampService));

            return timestampService;
        }

        public void Dispose()
        {
            _trustedTestCert?.Dispose();
            _trustedRepositoryCertificate?.Dispose();
            _trustedTestCertExpired?.Dispose();
            _trustedTestCertNotYetValid?.Dispose();
            _trustedServerRoot?.Dispose();
            _responders.Dispose();
            _certDir.Dispose();

            if (_trustedTestCertificateWithReissuedCertificate != null)
            {
                foreach (var certificate in _trustedTestCertificateWithReissuedCertificate)
                {
                    certificate.Dispose();
                }
            }

            if (_testServer.IsValueCreated)
            {
                _testServer.Value.Result.Dispose();
            }
        }
    }
}