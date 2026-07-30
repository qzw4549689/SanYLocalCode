using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Moq;
using SanyD365.Plugins.FactoryCredit;
using System;
using Xunit;

namespace FactoryCreditPlugins.Tests
{
    public class FcaProcActivationPluginTests
    {
        [Fact]
        public void GetCustomerCode_Should_Read_SapNumber_From_CustomerMasterData()
        {
            // Arrange
            var masterDataId = Guid.NewGuid();
            var expectedSapNumber = "0210000677";

            var mockService = new Mock<IOrganizationService>();
            mockService
                .Setup(s => s.Retrieve("mcs_customermasterdata", masterDataId, It.IsAny<ColumnSet>()))
                .Returns(new Entity("mcs_customermasterdata", masterDataId)
                {
                    ["mcs_sapnumber"] = expectedSapNumber
                });

            var mockTracer = new Mock<ITracingService>();
            var plugin = new FcaProcActivationPlugin();
            var accountRef = new EntityReference("mcs_customermasterdata", masterDataId);

            // Act
            string actual = plugin.GetCustomerCode(mockService.Object, mockTracer.Object, accountRef);

            // Assert
            Assert.Equal(expectedSapNumber, actual);
            mockService.Verify(s => s.Retrieve("mcs_customermasterdata", masterDataId, It.Is<ColumnSet>(c => c.Columns.Contains("mcs_sapnumber"))), Times.Once);
        }

        [Fact]
        public void GetCustomerCode_Should_Return_Empty_When_AccountRef_Is_Null()
        {
            var mockService = new Mock<IOrganizationService>();
            var mockTracer = new Mock<ITracingService>();
            var plugin = new FcaProcActivationPlugin();

            string actual = plugin.GetCustomerCode(mockService.Object, mockTracer.Object, null);

            Assert.Equal(string.Empty, actual);
            mockService.Verify(s => s.Retrieve(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<ColumnSet>()), Times.Never);
        }

        [Fact]
        public void GetCustomerCode_Should_Return_Empty_When_SapNumber_Is_Null()
        {
            var masterDataId = Guid.NewGuid();

            var mockService = new Mock<IOrganizationService>();
            mockService
                .Setup(s => s.Retrieve("mcs_customermasterdata", masterDataId, It.IsAny<ColumnSet>()))
                .Returns(new Entity("mcs_customermasterdata", masterDataId));

            var mockTracer = new Mock<ITracingService>();
            var plugin = new FcaProcActivationPlugin();
            var accountRef = new EntityReference("mcs_customermasterdata", masterDataId);

            string actual = plugin.GetCustomerCode(mockService.Object, mockTracer.Object, accountRef);

            Assert.Equal(string.Empty, actual);
        }
    }
}
