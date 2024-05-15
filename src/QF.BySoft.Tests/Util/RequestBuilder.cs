using System;
using System.Collections.Generic;
using MetalHeaven.Agent.Shared.External.Messages;
using UnitsNet;
using Versioned.ExternalDataContracts.Contracts.Article;
using Versioned.ExternalDataContracts.Contracts.BoM;
using Versioned.ExternalDataContracts.Contracts.Resource;
using Versioned.ExternalDataContracts.Enums;

namespace QF.BySoft.Tests.Util;

public static class RequestBuilder
{
    public static RequestManufacturabilityCheckOfPartTypeMessage GetRequest(Guid projectId, Guid partId, Uri stepFileUri, Guid machineId,
        string materialId, List<string> materialTokens, double thickness)
    {
        var request = new RequestManufacturabilityCheckOfPartTypeMessage
        {
            ProjectId = projectId,
            PartType = new PartTypeV1
            {
                Id = partId,
                Estimations = new List<EstimationsV1>
                {
                    new()
                    {
                        WorkingStepEstimations = new List<EstimationV1>
                        {
                            new()
                            {
                                ResourceWorkingStepKey = new ResourceWorkingStepKeyV1
                                {
                                    ResourceId = machineId,
                                    WorkingStepKey = new WorkingStepKeyV1(WorkingStepTypeV1.SheetBending, WorkingStepTypeV1.SheetBending)
                                }
                            }
                        }
                    }
                },
                Material = new ArticleDataV1
                {
                    SelectableArticles = new ArticleSelectablesV1
                    {
                        SelectedArticle = new ArticleSummaryV1
                        {
                            Dimensions = new DimensionsV1
                            {
                                Thickness = Length.FromMillimeters(thickness)
                            },
                            Tokens = materialTokens,
                            Id = materialId
                        }
                    }
                }
            },
            StepFileUrl = stepFileUri
        };

        return request;
    }
}
