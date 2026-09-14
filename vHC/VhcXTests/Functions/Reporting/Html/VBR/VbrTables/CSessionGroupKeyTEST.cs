using System;
using VeeamHealthCheck.Functions.Reporting.DataTypes;
using VeeamHealthCheck.Functions.Reporting.Html.VBR.VbrTables.Job_Session_Summary;
using Xunit;

namespace VhcXTests.Functions.Reporting.Html.VBR.VbrTables
{
    public class CSessionGroupKeyTEST
    {
        private static readonly Guid ParentId = Guid.Parse("02fe84bc-7394-42b5-bdb2-81a56190d8c5");
        private static readonly Guid ChildId  = Guid.Parse("592c44dc-861c-48fc-b70e-e9916c790222");

        [Fact]
        public void Of_ChildWithPolicyTag_ReturnsParentGuid()
        {
            var s = new CJobSessionInfo
            {
                JobName    = "Physical - Linux Servers - lab01",
                JobId      = ChildId,
                PolicyName = "Physical - Linux Servers",
                PolicyTag  = ParentId,
            };
            Assert.Equal("id:" + ParentId.ToString("D"), CSessionGroupKey.Of(s));
        }

        [Fact]
        public void Of_ParentSession_UsesOwnJobId()
        {
            var s = new CJobSessionInfo
            {
                JobName    = "Physical - Linux Servers",
                JobId      = ParentId,
                PolicyName = "Physical - Linux Servers",
                PolicyTag  = ParentId,  // equal to own JobId
            };
            Assert.Equal("id:" + ParentId.ToString("D"), CSessionGroupKey.Of(s));
        }

        [Fact]
        public void Of_BCParent_PolicyTagEmpty_UsesOwnJobId()
        {
            // BC orchestrator (SimpleBackupCopyPolicy) has empty PolicyName/PolicyTag,
            // per the probe-policy-link.csv evidence.
            var parentGuid = Guid.Parse("2b60f399-4be7-4548-937d-c9357d5b59e6");
            var s = new CJobSessionInfo
            {
                JobName    = "Backup Copy - Engineers CHC 02",
                JobId      = parentGuid,
                PolicyName = null,
                PolicyTag  = null,
            };
            Assert.Equal("id:" + parentGuid.ToString("D"), CSessionGroupKey.Of(s));
        }

        [Fact]
        public void Of_RegularBackup_NoChildrenNoPolicyTag_UsesOwnJobId()
        {
            // Hyper-V Backup case: PolicyName/PolicyTag empty.
            var jobId = Guid.Parse("68621a52-2a9c-4fc5-a3f4-acc1c2caa44e");
            var s = new CJobSessionInfo
            {
                JobName    = "Hyper-V - Engineers CHC 01",
                JobId      = jobId,
                PolicyName = null,
                PolicyTag  = null,
            };
            Assert.Equal("id:" + jobId.ToString("D"), CSessionGroupKey.Of(s));
        }

        [Fact]
        public void Of_LegacyCsv_NoGuids_FallsBackToJobName()
        {
            var s = new CJobSessionInfo
            {
                JobName    = "Legacy Job",
                JobId      = null,
                PolicyName = null,
                PolicyTag  = null,
            };
            Assert.Equal("name:Legacy Job", CSessionGroupKey.Of(s));
        }

        [Fact]
        public void DisplayName_PrefersPolicyName()
        {
            var s = new CJobSessionInfo
            {
                JobName    = "Physical - Linux Servers - lab01",
                PolicyName = "Physical - Linux Servers",
            };
            Assert.Equal("Physical - Linux Servers", CSessionGroupKey.DisplayName(s));
        }

        [Fact]
        public void DisplayName_FallsBackToJobName_WhenPolicyNameEmpty()
        {
            var s = new CJobSessionInfo
            {
                JobName    = "Hyper-V - Engineers CHC 01",
                PolicyName = "",
            };
            Assert.Equal("Hyper-V - Engineers CHC 01", CSessionGroupKey.DisplayName(s));
        }

        // Issue #219 regression tests: Backup-Copy per-object child sessions can carry
        // PolicyTag == JobId (a self-reference meaning "no parent GUID link"), so GUID
        // grouping alone leaves each child as its own row with a mangled
        // "<Parent>\<child>" display name. Group() adds a name-hierarchy rollup pass for
        // exactly that case, using only synthetic names/GUIDs below.

        [Fact]
        public void Group_SelfReferencingPolicyTagWithHierarchicalName_RollsUpUnderParent()
        {
            var parentName = "Site-A to Site-B - Bronze Copy";
            var childId1 = Guid.Parse("11111111-1111-1111-1111-111111111111");
            var childId2 = Guid.Parse("22222222-2222-2222-2222-222222222222");

            var s1 = new CJobSessionInfo
            {
                Name       = parentName + "\\ChildBackup - vm01",
                JobName    = parentName + "\\ChildBackup - vm01",
                PolicyName = parentName + "\\ChildBackup - vm01",
                JobId      = childId1,
                PolicyTag  = childId1, // self-reference: no parent GUID link
                DataSize   = 100,
                BackupSize = 50,
            };
            var s2 = new CJobSessionInfo
            {
                Name       = parentName + "\\ChildBackup - vm02",
                JobName    = parentName + "\\ChildBackup - vm02",
                PolicyName = parentName + "\\ChildBackup - vm02",
                JobId      = childId2,
                PolicyTag  = childId2, // self-reference: no parent GUID link
                DataSize   = 200,
                BackupSize = 75,
            };

            var groups = CSessionGroupKey.Group(new[] { s1, s2 });

            Assert.Single(groups);
            Assert.Equal(parentName, groups[0].DisplayName);
            Assert.Equal(2, groups[0].Sessions.Count);
        }

        [Fact]
        public void Group_GuidLinkedChild_StillRollsUpUnderParentGuid()
        {
            // Confirms the pre-existing GUID-based rollup path is unaffected by the new pass.
            var parentId = Guid.Parse("11111111-1111-1111-1111-111111111111");
            var childId = Guid.Parse("22222222-2222-2222-2222-222222222222");

            var parent = new CJobSessionInfo
            {
                Name       = "ParentJob",
                JobName    = "ParentJob",
                PolicyName = null,
                JobId      = parentId,
                PolicyTag  = parentId, // equal to own JobId
            };
            var child = new CJobSessionInfo
            {
                Name       = "ParentJob - vm01",
                JobName    = "ParentJob - vm01",
                PolicyName = "ParentJob",
                JobId      = childId,
                PolicyTag  = parentId, // real GUID link to parent
            };

            var groups = CSessionGroupKey.Group(new[] { parent, child });

            Assert.Single(groups);
            Assert.Equal("id:" + parentId.ToString("D"), groups[0].Key);
            Assert.Equal(2, groups[0].Sessions.Count);
        }

        [Fact]
        public void Group_DataBearingParentWithHierarchicalChild_StillMerges()
        {
            // Was "DoesNotMerge" -- this test used to pin the identityHasData guard
            // (removed in ADR 0020/0030): a parent identity that carries data of its
            // own used to keep its own row instead of absorbing a same-prefix child.
            // That guard was found to backfire on the confirmed real-world case where a
            // Backup Copy job has a GUID-linked child with data AND a self-referencing
            // hierarchical child with data for the same parent in the same window (see
            // the regression tests below and ADR 0030) -- so the assertion is flipped
            // here to match the corrected behavior: a prefix match always merges now,
            // regardless of whether the parent identity has data of its own.
            var parentName = "Site-A to Site-B - Bronze Copy";
            var parentId = Guid.Parse("11111111-1111-1111-1111-111111111111");
            var childId = Guid.Parse("22222222-2222-2222-2222-222222222222");

            var parent = new CJobSessionInfo
            {
                Name       = parentName,
                JobName    = parentName,
                PolicyName = null,
                JobId      = parentId,
                PolicyTag  = parentId, // self-reference
                DataSize   = 500,      // parent carries data of its own -- no longer blocks the merge
                BackupSize = 250,
            };
            var child = new CJobSessionInfo
            {
                Name       = parentName + "\\ChildBackup - vm01",
                JobName    = parentName + "\\ChildBackup - vm01",
                PolicyName = parentName + "\\ChildBackup - vm01",
                JobId      = childId,
                PolicyTag  = childId, // self-reference
                DataSize   = 100,
                BackupSize = 50,
            };

            var groups = CSessionGroupKey.Group(new[] { parent, child });

            Assert.Single(groups);
            Assert.Equal(parentName, groups[0].DisplayName);
            Assert.Equal(2, groups[0].Sessions.Count);
        }

        // Follow-up regression (found during independent PR review, not issue #219
        // itself): the identityHasData guard above reproduced issue #219's symptom for
        // the case the hierarchical pass exists to fix. A Backup Copy job's GUID-linked
        // child (real, different PolicyTag) and its self-referencing hierarchical child
        // (PolicyTag == own JobId) are both normal, healthy children of the same job and
        // routinely both carry data in the same reporting window -- not a rare
        // coincidence. The guard saw the GUID-linked child's data under the shared
        // "ParentJob" identity and refused to merge the hierarchical child into it,
        // kicking it out into its own mangled "ParentJob\..." row. See ADR 0030.

        [Fact]
        public void Group_GuidLinkedSiblingWithData_HierarchicalSelfReferencingChildStillMerges()
        {
            var parentJobId = Guid.Parse("22222222-2222-2222-2222-222222222222");
            var guidLinkedChildId = Guid.Parse("11111111-1111-1111-1111-111111111111");
            var selfRefChildId = Guid.Parse("33333333-3333-3333-3333-333333333333");

            var orchestrator = new CJobSessionInfo
            {
                Name       = "ParentJob",
                JobName    = "ParentJob",
                JobId      = parentJobId,
                PolicyTag  = parentJobId, // self-reference: own row, no rollup needed
                PolicyName = null,
                DataSize   = 0,
                BackupSize = 0,
            };
            var guidLinkedChild = new CJobSessionInfo
            {
                Name       = "GuidLinkedChild",
                JobName    = "GuidLinkedChild",
                JobId      = guidLinkedChildId,
                PolicyTag  = parentJobId, // real GUID link to a different job (the parent)
                PolicyName = "ParentJob", // no backslash
                DataSize   = 100,
                BackupSize = 50,
            };
            var selfRefChild = new CJobSessionInfo
            {
                Name       = "ParentJob\\ChildVm01 Backup",
                JobName    = "ParentJob\\ChildVm01 Backup",
                JobId      = selfRefChildId,
                PolicyTag  = selfRefChildId, // self-reference: no parent GUID link at all
                PolicyName = null,           // DisplayName falls back to JobName
                DataSize   = 200,
                BackupSize = 75,
            };

            var groups = CSessionGroupKey.Group(new[] { orchestrator, guidLinkedChild, selfRefChild });

            Assert.Single(groups);
            Assert.Equal("ParentJob", groups[0].DisplayName);
            Assert.Equal(3, groups[0].Sessions.Count);
            Assert.Contains(guidLinkedChild, groups[0].Sessions);
            Assert.Contains(selfRefChild, groups[0].Sessions);
        }

        [Fact]
        public void Group_MultipleSelfReferencingChildren_AllMergeUnderSameDataBearingParentIdentity()
        {
            // Extends the regression above: a second self-referencing hierarchical child
            // under the same parent identity also merges -- the fix isn't limited to
            // absorbing a single hierarchical child per parent.
            var parentJobId = Guid.Parse("22222222-2222-2222-2222-222222222222");
            var guidLinkedChildId = Guid.Parse("11111111-1111-1111-1111-111111111111");
            var selfRefChildId1 = Guid.Parse("33333333-3333-3333-3333-333333333333");
            var selfRefChildId2 = Guid.Parse("44444444-4444-4444-4444-444444444444");

            var orchestrator = new CJobSessionInfo
            {
                Name       = "ParentJob",
                JobName    = "ParentJob",
                JobId      = parentJobId,
                PolicyTag  = parentJobId,
                PolicyName = null,
                DataSize   = 0,
                BackupSize = 0,
            };
            var guidLinkedChild = new CJobSessionInfo
            {
                Name       = "GuidLinkedChild",
                JobName    = "GuidLinkedChild",
                JobId      = guidLinkedChildId,
                PolicyTag  = parentJobId,
                PolicyName = "ParentJob",
                DataSize   = 100,
                BackupSize = 50,
            };
            var selfRefChild1 = new CJobSessionInfo
            {
                Name       = "ParentJob\\ChildVm01 Backup",
                JobName    = "ParentJob\\ChildVm01 Backup",
                JobId      = selfRefChildId1,
                PolicyTag  = selfRefChildId1,
                PolicyName = null,
                DataSize   = 200,
                BackupSize = 75,
            };
            var selfRefChild2 = new CJobSessionInfo
            {
                Name       = "ParentJob\\ChildVm02 Backup",
                JobName    = "ParentJob\\ChildVm02 Backup",
                JobId      = selfRefChildId2,
                PolicyTag  = selfRefChildId2,
                PolicyName = null,
                DataSize   = 300,
                BackupSize = 90,
            };

            var groups = CSessionGroupKey.Group(new[] { orchestrator, guidLinkedChild, selfRefChild1, selfRefChild2 });

            Assert.Single(groups);
            Assert.Equal("ParentJob", groups[0].DisplayName);
            Assert.Equal(4, groups[0].Sessions.Count);
        }

        [Fact]
        public void Group_ZeroDataParentWithHierarchicalChild_Merges()
        {
            // A parent identity with zero data of its own does not block the rollup.
            var parentName = "Site-A to Site-B - Bronze Copy";
            var parentId = Guid.Parse("11111111-1111-1111-1111-111111111111");
            var childId = Guid.Parse("22222222-2222-2222-2222-222222222222");

            var parent = new CJobSessionInfo
            {
                Name       = parentName,
                JobName    = parentName,
                PolicyName = null,
                JobId      = parentId,
                PolicyTag  = parentId, // self-reference
                DataSize   = 0,
                BackupSize = 0,
            };
            var child = new CJobSessionInfo
            {
                Name       = parentName + "\\ChildBackup - vm01",
                JobName    = parentName + "\\ChildBackup - vm01",
                PolicyName = parentName + "\\ChildBackup - vm01",
                JobId      = childId,
                PolicyTag  = childId, // self-reference
                DataSize   = 100,
                BackupSize = 50,
            };

            var groups = CSessionGroupKey.Group(new[] { parent, child });

            Assert.Single(groups);
            Assert.Equal(parentName, groups[0].DisplayName);
            Assert.Equal(2, groups[0].Sessions.Count);
        }

        [Fact]
        public void Group_UnrelatedStandaloneSessions_ProducesSeparateGroups()
        {
            var job1Id = Guid.Parse("11111111-1111-1111-1111-111111111111");
            var job2Id = Guid.Parse("22222222-2222-2222-2222-222222222222");

            var s1 = new CJobSessionInfo
            {
                Name       = "Job1",
                JobName    = "Job1",
                PolicyName = null,
                JobId      = job1Id,
                PolicyTag  = job1Id,
            };
            var s2 = new CJobSessionInfo
            {
                Name       = "Job2",
                JobName    = "Job2",
                PolicyName = null,
                JobId      = job2Id,
                PolicyTag  = job2Id,
            };

            var groups = CSessionGroupKey.Group(new[] { s1, s2 });

            Assert.Equal(2, groups.Count);
            Assert.Contains(groups, g => g.DisplayName == "Job1");
            Assert.Contains(groups, g => g.DisplayName == "Job2");
        }
    }
}
