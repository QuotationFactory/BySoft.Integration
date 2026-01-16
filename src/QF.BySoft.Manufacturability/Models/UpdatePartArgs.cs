#nullable enable
namespace QF.BySoft.Manufacturability.Models;

// {
//   "name": "string",
//   "uri": "string",
//   "description": "string",
//   "userInfo1": "string",
//   "userInfo2": "string",
//   "userInfo3": "string",
//   "materialName": "string",
//   "cuttingMachineName": "string",
//   "bendingMachineName": "string",
//   "thickness": 0,
//   "length": 0,
//   "width": 0,
//   "effectiveArea": 0,
//   "exteriorArea": 0,
//   "rectangularArea": 0,
//   "effectiveWeight": 0,
//   "exteriorWeight": 0,
//   "rectangularWeight": 0,
//   "totalCuttingTimeSeconds": 0,
//   "totalBendingTimeSeconds": 0,
//   "bendingRotationsCountAxisX": 0,
//   "bendingRotationsCountAxisY": 0,
//   "bendingRotationsCountAxisZ": 0,
//   "smallImagePng": "string",
//   "largeImagePng": "string",
//   "largeImagePng3D": "string",
//   "numberOfEmployees": 0,
//   "bendLines": [
//     {
//       "length": 0,
//       "angle": 0,
//       "radius": 0,
//       "kFactor": 0
//     }
//   ],
//   "cuttingLength": 0,
//   "numberOfContours": 0,
//   "cuttingGas": "string",
//   "links": [
//     "string"
//   ],
//   "toolSetups": [
//     {
//       "id": 0,
//       "stations": [
//         {
//           "id": 0,
//           "topTool": {
//             "name": "string",
//             "segmentTypes": [
//               {}
//             ],
//             "segments": [
//               "string"
//             ]
//           },
//           "lowerTopToolAdapter": {
//             "name": "string",
//             "segmentTypes": [
//               {}
//             ],
//             "segments": [
//               "string"
//             ]
//           },
//           "upperTopToolAdapter": {
//             "name": "string",
//             "segmentTypes": [
//               {}
//             ],
//             "segments": [
//               "string"
//             ]
//           },
//           "bottomTool": {
//             "name": "string",
//             "segmentTypes": [
//               {}
//             ],
//             "segments": [
//               "string"
//             ]
//           },
//           "lowerBottomToolAdapter": {
//             "name": "string",
//             "segmentTypes": [
//               {}
//             ],
//             "segments": [
//               "string"
//             ]
//           },
//           "upperBottomToolAdapter": {
//             "name": "string",
//             "segmentTypes": [
//               {}
//             ],
//             "segments": [
//               "string"
//             ]
//           }
//         }
//       ]
//     }
//   ],
//   "ncParameterFile": "string"
// }

public class UpdatePartArgs
{
    // public string Name { get; set; }
    public required string? Description { get; set; }
    public required string? UserInfo1 { get; set; }
    public required string? UserInfo2 { get; set; }
    public required string? UserInfo3 { get; set; }
    public required string MaterialName { get; set; }
    public required string CuttingMachineName { get; set; }
    public required string BendingMachineName { get; set; }
    public double Thickness { get; set; }
    public required int Priority { get; set; }
    public required int? RotationAllowance { get; set; }
}
